using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace UNOCardGame
{
    /// <summary>
    /// Classe che implementa la serializzazione/deserializzazione JSON.
    /// </summary>
    public abstract class Serialization<T> where T: Serialization<T>
    {
        /// <summary>
        /// Serializza la classe in JSON sotto forma di stringa.
        /// </summary>
        /// <returns>Stringa contenente la classe sotto forma di JSON</returns>
        public string Serialize() => JsonSerializer.Serialize((T)this);

        /// <summary>
        /// Deserializza i dati JSON sotto forma di stringa e li trasforma nella classe. 
        /// </summary>
        /// <param name="json">JSON da deserializzare</param>
        /// <returns>La classe deserializzata</returns>
        public static T Deserialize(string json) => JsonSerializer.Deserialize<T>(json);

        /// <summary>
        /// Serializza la classe e la trasforma in bytes (UTF-8).
        /// </summary>
        /// <returns>I byte della classe</returns>
        public byte[] Encode() => Encoding.UTF8.GetBytes(Serialize());

        /// <summary>
        /// Deserializza i byte (UTF-8) e li trasforma nella classe.
        /// </summary>
        /// <returns>La classe deserializzata</returns>
        public static T Decode(byte[] bytes) => Deserialize(Encoding.UTF8.GetString(bytes));
    }
}
