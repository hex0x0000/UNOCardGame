using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace UNOCardGame
{
    /// <summary>
    /// Classe per il logging.
    /// Utilizzabile da qualsiasi parte nel codice.
    /// </summary>
    public class Log
    {
        /// <summary>
        /// Ritorna il path della funzione in cui è stata chiamata la funzione di logging
        /// </summary>
        /// <returns></returns>
        private static string CallLocation()
        {
            List<StackFrame> frames = new StackTrace(false).GetFrames().ToList();
            frames.RemoveRange(0, 3);
            frames.Reverse();
            return String.Join("::", frames.ConvertAll(frame => frame.GetMethod().Name));
        }

        /// <summary>
        /// Messaggio comune per tutte le funzioni
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private static string Base(string message) => $"{DateTime.Now} - {CallLocation()} -> {message}";

        private static string Base(Socket socket, string message) => $"{DateTime.Now} - {CallLocation()} - Connection: {socket} -> {message}";

        public static void Info(string message) => Console.WriteLine($"[INFO] {Base(message)}");

        public static void Info(Socket socket, string message) => Console.WriteLine($"[INFO] {Base(socket, message)}");

        public static void Warn(string message) => Console.WriteLine($"[WARN] {Base(message)}");

        public static void Warn(Socket socket, string message) => Console.WriteLine($"[WARN] {Base(socket, message)}");

        public static void Error(string message) => Console.WriteLine($"[ERROR] {Base(message)}");

        public static void Error(Socket socket, string message) => Console.WriteLine($"[ERROR] {Base(socket, message)}");
    }
}
