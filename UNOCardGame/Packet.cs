using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace UNOCardGame
{

    public enum PacketExceptions
    {
        SerializationFailed,
        DeserializationFailed,
        EncodingFailed,
        DecodingFailed,
        SocketFailed,
        PacketTooBig,
        InvalidArgument,
        Unknown
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

        public override string ToString() => $"{ExceptionType}: {Message}. Inner exception was {InnerException.GetType()}: {InnerException.Message}\nStacktrace: {StackTrace}";
    }

    /// <summary>
    /// Un pacchetto con un contenuto generico.
    /// Questa classe contiene tutte le funzioni necessarie per mandare un pacchetto
    /// di informazioni, sia per il client che per il server.
    /// Il massimo di byte possibili in un pacchetto è di 65535 bytes.
    /// </summary>
    /// <typeparam name="T">Una classe che può essere serializzata</typeparam>
    public class Packet<T> where T : Serialization<T>
    {
        /// <summary>
        /// Contenuto del pacchetto.
        /// Questo contenuto può essere serializzato e codificato per essere mandato.
        /// </summary>
        public T Content { get; protected set; }

        public Packet(T content) => Content = content;

        /// <summary>
        /// Manda il numero di byte del contenuto da mandare.
        /// </summary>
        /// <param name="socket">Socket della connessione</param>
        /// <param name="n">Numero da mandare</param>
        private static void SendContentLen(Socket socket, ushort n)
        {
            byte[] buffer = BitConverter.GetBytes(n);
            // Inverte l'ordine a big endian se l'architettura è little endian
            if (BitConverter.IsLittleEndian)
                Array.Reverse(buffer);
            socket.Send(buffer);
        }

        /// <summary>
        /// Riceve il numero di byte del contenuto da ricevere.
        /// </summary>
        /// <param name="socket">Socket della connessione</param>
        /// <returns>Numero di byte da ricevere</returns>
        private static ushort ReceiveContentLen(Socket socket)
        {
            byte[] buffer = new byte[sizeof(ushort)];
            socket.Receive(buffer);
            // Inverte l'ordine da big endian se l'architettura del PC è little endian
            if (BitConverter.IsLittleEndian)
                Array.Reverse(buffer);
            return BitConverter.ToUInt16(buffer, 0);
        }

        /// <summary>
        /// Manda il pacchetto.
        /// </summary>
        /// <param name="socket">Socket della connessione a cui mandare il pacchetto</param>
        public void Send(Socket socket)
        {
            try
            {
                // Manda nome del pacchetto
                byte[] name = Encoding.UTF8.GetBytes(nameof(T));
                SendContentLen(socket, (ushort)name.Length);
                socket.Send(name);

                // Manda contenuto del pacchetto
                byte[] content = Content.Encode();
                SendContentLen(socket, (ushort)content.Length);
                socket.Send(content);
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
            catch (Exception e)
            {
                throw new PacketException(PacketExceptions.Unknown, "Unknown exception happened while sending packet", e);
            }
        }

        /// <summary>
        /// Riceve il nome del pacchetto che sta venendo ricevuto.
        /// </summary>
        /// <param name="socket">Socket della connessione da cui ricevere il nome</param>
        /// <returns></returns>
        public static string ReceiveName(Socket socket)
        {
            try
            {
                byte[] name = new byte[ReceiveContentLen(socket)];
                socket.Receive(name);
                return Encoding.UTF8.GetString(name);
            }
            catch (ArgumentNullException e)
            {
                throw new PacketException(PacketExceptions.InvalidArgument, "A passed argument was null while receiving name", e);
            }
            catch (SocketException e)
            {
                throw new PacketException(PacketExceptions.SocketFailed, "Failed to receive name due to connection error", e);
            }
            catch (DecoderFallbackException e)
            {
                throw new PacketException(PacketExceptions.DecodingFailed, "Failed to decode packet name", e);
            }
            catch (Exception e)
            {
                throw new PacketException(PacketExceptions.Unknown, "Unknown exception happened while receiving name", e);
            }
        }

        /// <summary>
        /// Riceve il pacchetto con il contenuto.
        /// </summary>
        /// <param name="socket">Socket della connessione da cui ricevere il pacchetto</param>
        /// <returns>Il pacchetto ricevuto</returns>
        public static Packet<T> Receive(Socket socket)
        {
            try
            {
                byte[] content = new byte[ReceiveContentLen(socket)];
                socket.Receive(content);
                T contentDecoded = Serialization<T>.Decode(content);
                return new Packet<T>(contentDecoded);
            }
            catch (ArgumentNullException e)
            {
                throw new PacketException(PacketExceptions.InvalidArgument, "A passed argument was null while receiving packet", e);
            }
            catch (SocketException e)
            {
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
            catch (Exception e)
            {
                throw new PacketException(PacketExceptions.Unknown, "Unknown exception happened while receiving packet", e);
            }
        }
    }
}
