using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UNOCardGame
{
    /// <summary>
    /// Possibili errori durante la comunicazione dei pacchetti.
    /// </summary>
    public enum PacketExceptions
    {
        ConnectionClosed,
        SerializationFailed,
        DeserializationFailed,
        EncodingFailed,
        DecodingFailed,
        SocketFailed,
        PacketTooBig,
        InvalidArgument
    }

    /// <summary>
    /// Errore durante la comunicazione di un pacchetto.
    /// </summary>
    public class PacketException : Exception
    {
        public PacketExceptions ExceptionType { get; }

        public PacketException(PacketExceptions exception, string message, Exception inner) : base(message, inner)
        {
            ExceptionType = exception;
        }

        public override string ToString() => $"{ExceptionType}: {Message}.{Environment.NewLine}Inner exception was {InnerException.GetType()}: {InnerException.Message}{Environment.NewLine}{InnerException}{Environment.NewLine}Stacktrace: {StackTrace}";
    }

    enum PacketType : short
    {
        Join,
        JoinStatus,
        PlayerUpdate,
        ChatMessage,
        ConnectionEnd,
        TurnUpdate,
        CardsUpdate
    }

    /// <summary>
    /// Classe contenente tutte le funzioni necessarie per mandare e ricevere pacchetti.
    /// La grandezza massima di un pacchetto è sizeof(ushort) (65535 bytes)
    /// </summary>
    public static class Packet
    {
        /// <summary>
        /// Manda il numero di byte del contenuto da mandare.
        /// </summary>
        /// <param name="socket">Socket della connessione</param>
        /// <param name="n">Numero da mandare</param>
        private async static Task SendContentLen(Socket socket, ushort n)
        {
            byte[] buffer = BitConverter.GetBytes(n);
            // Inverte l'ordine a big endian se l'architettura è little endian
            if (BitConverter.IsLittleEndian)
                Array.Reverse(buffer);
            await socket.SendAsync(buffer);
        }

        /// <summary>
        /// Riceve il numero di byte del contenuto da ricevere.
        /// </summary>
        /// <param name="socket">Socket della connessione</param>
        /// <returns>Numero di byte da ricevere</returns>
        private async static Task<ushort> ReceiveContentLen(Socket socket)
        {
            byte[] buffer = new byte[sizeof(ushort)];
            await socket.ReceiveAsync(buffer);
            // Inverte l'ordine da big endian se l'architettura del PC è little endian
            if (BitConverter.IsLittleEndian)
                Array.Reverse(buffer);
            return BitConverter.ToUInt16(buffer, 0);
        }

        /// <summary>
        /// Annulla la ricezione di un pacchetto.
        /// </summary>
        /// <param name="socket">Connessione su cui annullare la ricezione</param>
        /// <exception cref="PacketException"></exception>
        public async static Task CancelReceive(Socket socket)
        {
            try
            {
                byte[] name = new byte[await ReceiveContentLen(socket)];
                await socket.ReceiveAsync(name);
            }
            catch (ArgumentNullException e)
            {
                throw new PacketException(PacketExceptions.InvalidArgument, "A passed argument was null while receiving packet", e);
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.ConnectionReset || e.SocketErrorCode == SocketError.ConnectionAborted || e.SocketErrorCode == SocketError.ConnectionRefused)
                    throw new PacketException(PacketExceptions.ConnectionClosed, "ConnectionEnd was closed.", e);
                throw new PacketException(PacketExceptions.SocketFailed, "Failed to receive name due to connection error", e);
            }
        }

        /// <summary>
        /// Riceve il tipo del pacchetto che sta venendo ricevuto.
        /// </summary>
        /// <param name="socket">Socket della connessione da cui ricevere il tipo di pacchetto</param>
        /// <returns>L'ID del tipo del pacchetto</returns>
        public async static Task<short> ReceiveType(Socket socket, CancellationToken? canc)
        {
            try
            {
                byte[] type = new byte[sizeof(short)];
                if (canc is CancellationToken _canc)
                    await socket.ReceiveAsync(type, _canc);
                else
                    await socket.ReceiveAsync(type);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(type);
                return BitConverter.ToInt16(type, 0);
            }
            catch (ArgumentNullException e)
            {
                throw new PacketException(PacketExceptions.InvalidArgument, "A passed argument was null while receiving name", e);
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.ConnectionReset || e.SocketErrorCode == SocketError.ConnectionAborted || e.SocketErrorCode == SocketError.ConnectionRefused)
                    throw new PacketException(PacketExceptions.ConnectionClosed, "ConnectionEnd was closed.", e);
                throw new PacketException(PacketExceptions.SocketFailed, "Failed to receive name due to connection error", e);
            }
            catch (DecoderFallbackException e)
            {
                throw new PacketException(PacketExceptions.DecodingFailed, "Failed to decode packet name", e);
            }
        }

        /// <summary>
        /// Manda il pacchetto. Se il contenuto del pacchetto è nullo, manda solo l'ID del pacchetto (packetType).
        /// Sia il contenuto che il tipo del pacchetto non possono essere null contemporaneamente.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="socket">Connessione a cui mandare il pacchetto</param>
        /// <param name="content">Contenuto del pacchetto</param>
        /// <param name="packetType">Tipo del pacchetto</param>
        /// <exception cref="PacketException"></exception>
        public async static Task Send<T>(Socket socket, T content, short? packetType = null) where T : Serialization<T>
        {
            try
            {
                // Manda il nome del pacchetto
                byte[] type;
                if (content is T _contentValue)
                    type = BitConverter.GetBytes(_contentValue.PacketId);
                else if (packetType is short packetTypeValue)
                    type = BitConverter.GetBytes(packetTypeValue);
                else
                    throw new ArgumentNullException("Both content and packet type are null.");
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(type);
                await socket.SendAsync(type);

                // Manda il contenuto del pacchetto (se c'è)
                if (content is T contentValue)
                {
                    byte[] contentBuf = contentValue.Encode();
                    await SendContentLen(socket, (ushort)contentBuf.Length);
                    await socket.SendAsync(contentBuf);
                }
            }
            catch (OverflowException e)
            {
                throw new PacketException(PacketExceptions.PacketTooBig, "Packet too big, overflow happened while sending", e);
            }
            catch (ArgumentNullException e)
            {
                throw new PacketException(PacketExceptions.InvalidArgument, "A passed argument was null while sending packet", e);
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.ConnectionReset || e.SocketErrorCode == SocketError.ConnectionAborted || e.SocketErrorCode == SocketError.ConnectionRefused)
                    throw new PacketException(PacketExceptions.ConnectionClosed, "ConnectionEnd was closed.", e);
                throw new PacketException(PacketExceptions.SocketFailed, "Failed to send packet due to connection error", e);
            }
            catch (NotSupportedException e)
            {
                throw new PacketException(PacketExceptions.SerializationFailed, "Failed to serialize packet while sending it", e);
            }
            catch (EncoderFallbackException e)
            {
                throw new PacketException(PacketExceptions.EncodingFailed, "Failed to encode packet while sending it", e);
            }
        }

        /// <summary>
        /// Riceve il contenuto del pacchetto.
        /// </summary>
        /// <param name="socket">Socket della connessione da cui ricevere il pacchetto</param>
        /// <returns>Il pacchetto ricevuto</returns>
        public async static Task<T> Receive<T>(Socket socket) where T : Serialization<T>
        {
            try
            {
                byte[] content = new byte[await ReceiveContentLen(socket)];
                await socket.ReceiveAsync(content);
                Debug.WriteLine($"Content: {Encoding.UTF8.GetString(content)}");
                return Serialization<T>.Decode(content);
            }
            catch (ArgumentNullException e)
            {
                throw new PacketException(PacketExceptions.InvalidArgument, "A passed argument was null while receiving packet", e);
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.ConnectionReset || e.SocketErrorCode == SocketError.ConnectionAborted || e.SocketErrorCode == SocketError.ConnectionRefused)
                    throw new PacketException(PacketExceptions.ConnectionClosed, "ConnectionEnd was closed.", e);
                throw new PacketException(PacketExceptions.SocketFailed, "Failed to receive packet due to connection error", e);
            }
            catch (DecoderFallbackException e)
            {
                throw new PacketException(PacketExceptions.DecodingFailed, "Failed to decode packet", e);
            }
            catch (JsonException e)
            {
                throw new PacketException(PacketExceptions.DeserializationFailed, "Failed to deserialize packet's JSON while receiving it", e);
            }
            catch (NotSupportedException e)
            {
                throw new PacketException(PacketExceptions.DeserializationFailed, "Failed to deserialize packet while receiving it, not supported", e);
            }
        }
    }
}
