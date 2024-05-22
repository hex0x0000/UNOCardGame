using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UNOCardGame
{
    /// <summary>
    /// Classe per il logging.
    /// Utilizzabile da qualsiasi parte nel codice.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class Logger
    {
        /// <summary>
        /// Definisce quale parte del programma sta usando questo logger
        /// </summary>
        private string Type = null;

        /// <summary>
        /// Task che si occupa di scrivere sul file i log
        /// </summary>
        private Task LogTask = null;

        /// <summary>
        /// Comunicatore che manda i nuovi log al logs writer
        /// </summary>
        private ChannelWriter<string> Writer = null;

        /// <summary>
        /// Scrive i log su file
        /// </summary>
        /// <param name="read"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        private static async Task LogWriter(ChannelReader<string> read, string filename)
        {
            FileStream file;
            try
            {
                file = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write);
                Debug.WriteLine($"File name: {file.Name}");
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[ERROR] Failed to open log file: {e}");
                return;
            }
            while (true)
            {
                try
                {
                    string log = await read.ReadAsync();
                    if (log == null)
                        break;
                    await file.WriteAsync(Encoding.UTF8.GetBytes(log));
                    await file.FlushAsync();
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"[ERROR] Failed to log to file: {e}");
                    file.Dispose();
                    return;
                }
            }
            file.Dispose();
        }

        /// <summary>
        /// Inizializza il logger
        /// </summary>
        /// <param name="type">Specifica se si tratta del contesto del server</param>
        public Logger(string type)
        {
            try
            {
                // Crea la directory dei log se non esiste già
                string logDir = Application.UserAppDataPath + "\\logs";
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);

                // Crea il Channel per comunicare con il LogWriter
                Channel<string> channel = Channel.CreateUnbounded<string>();
                Writer = channel.Writer;

                // Avvia il Task
                LogTask = new Task(async () => await LogWriter(channel.Reader, logDir + $"\\{type}-{DateTime.Now.ToString("dd-MM-yy_HH-mm-ss")}.txt"));
                LogTask.Start();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[ERROR] Failed to start file logging: {e}");
                Writer = null;
                LogTask = null;
            }
            Type = type;
        }

        ~Logger()
        {
            Close();
        }

        /// <summary>
        /// Chiude il logger
        /// </summary>
        public void Close()
        {
            if (Writer != null)
            {
                Writer.TryWrite(null);
                Writer.Complete();
                Writer = null;
            }

            if (LogTask != null)
            {
                if (!LogTask.IsCompleted)
                    LogTask.Wait();
                LogTask.Dispose();
                LogTask = null;
            }
        }

        /// <summary>
        /// Ritorna il path della funzione in cui è stata chiamata la funzione di logging
        /// </summary>
        /// <returns></returns>
        private static string CallLocation()
        {
            List<StackFrame> frames = new StackTrace(false).GetFrames().ToList();
            frames.RemoveRange(0, 3);
            frames.Reverse();
            frames.RemoveRange(0, frames.Count - 5);
            return String.Join("::", frames.ConvertAll(frame => frame.GetMethod().Name));
        }

        /// <summary>
        /// Trasforma il socket in un indirizzo leggibile in forma IP:PORTA
        /// </summary>
        /// <param name="socket">Socket da trasformare in stringa</param>
        /// <returns></returns>
        public static string ToAddress(Socket socket) => ((IPEndPoint)socket.RemoteEndPoint).ToString();

        /// <summary>
        /// Funzione base che effettua il logging, le altre sono un'interfaccia per questa funzione
        /// </summary>
        /// <param name="logType">Tipo del log</param>
        /// <param name="message">Messaggio del log</param>
        /// <param name="socket">Socket (opzionale)</param>
        private void Base(string logType, string message, string socket)
        {
            string log = $"[{logType}] [{Type}] {DateTime.Now} - {CallLocation()}";
            if (socket != null)
                log += $" - Connection : {socket}";
            log += $" -> {message}{Environment.NewLine}";
            if (Writer is var writer)
                writer.TryWrite(log);
            Debug.Write(log);
        }

        public void Info(string message) => Base("INFO", message, null);

        public void Info(string socket, string message) => Base("INFO", message, socket);

        public void Info(Socket socket, string message) => Base("INFO", message, ToAddress(socket));

        public void Warn(string message) => Base("WARN", message, null);

        public void Warn(string socket, string message) => Base("WARN", message, socket);

        public void Warn(Socket socket, string message) => Base("WARN", message, ToAddress(socket));

        public void Error(string message) => Base("ERROR", message, null);

        public void Error(string socket, string message) => Base("ERROR", message, socket);

        public void Error(Socket socket, string message) => Base("ERROR", message, ToAddress(socket));
    }
}
