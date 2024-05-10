using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace UNOCardGame
{
    internal class Server
    {
        /// <summary>
        /// Il server continua ad aspettare connessioni finché questa flag non viene messa a false.
        /// </summary>
        private bool runFlag = true;

        /// <summary>
        /// Il socket del server.
        /// Viene usato per la comunicazione con i client.
        /// </summary>
        private Socket socket;

        /// <summary>
        /// Tiene conto del numero degli id.
        /// Il numero degli ID deve essere ordinato per mantenere l'ordine dei turni.
        /// </summary>
        private int idCount;

        /// <summary>
        /// Tutti i player del gioco, a parte l'host.
        /// Questo hashmap contiene tutti i dati necessari per comunicare con i client.
        /// </summary>
        private Dictionary<uint, PlayerData> players = new Dictionary<uint, PlayerData>();

        /// <summary>
        /// I dati di ogni player.
        /// Contiene il codice di accesso, il socket per comunicare con il client
        /// e le informazioni del player.
        /// </summary>
        private struct PlayerData
        {
            /// <summary>
            /// Il codice di accesso è necessario per evitare impersonificazioni.
            /// E' necessario anche in caso di riconessione.
            /// </summary>
            long accessCode;

            /// <summary>
            /// Socket della connessione al client. 
            /// </summary>
            Socket socket;

            /// <summary>
            /// Dati del player non legati alla connessione.
            /// </summary>
            Player player;
        }

        public Server(string address, short port)
        {
            IPEndPoint ipEndpoint = new IPEndPoint(IPAddress.Parse(address), port);
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(ipEndpoint);
        }

        /*
        private async string ReceivePacket(Task<Socket> client)
        {
            var buffer = new byte[4];
            int sizeReceived = await client.ReceiveAsync(buffer, SocketFlags.None);
        }
        
        public async void Listen()
        {
            socket.Listen(1000);
            while (runFlag)
            {
                var client = await socket.AcceptAsync();
                var buffer = new byte[4];
                int sizeReceived = await client.ReceiveAsync(buffer);
            }
        }*/
    }
}
