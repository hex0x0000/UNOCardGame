using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UNOCardGame.Packets
{
    /// <summary>
    /// Risultato di una richiesta.
    /// </summary>
    public enum StatusCode
    {
        Success,
        Error
    }

    /// <summary>
    /// Risultato di una richiesta.
    /// Contiene due payload a seconda se la richiesta è andata bene o se c'è stato un errore.
    /// </summary>
    /// <typeparam name="T">Tipo del payload di StatusCode.Success</typeparam>
    /// <typeparam name="E">Tipo del payload di StatusCode.Error</typeparam>
    public class Status<T, E> : Serialization<Status<T, E>> where T : class where E : class
    {
        private static readonly int _StatusCodeEnumLength = Enum.GetValues(typeof(JoinType)).Length;

        private StatusCode _Code;

        /// <summary>
        /// Specifica se la richiesta è andata bene o male.
        /// </summary>
        public StatusCode Code
        {
            get => _Code; private set
            {
                if ((int)value < 0 || (int)value >= _StatusCodeEnumLength)
                    throw new ArgumentOutOfRangeException(nameof(JoinType), "Enum must stay within its range");
                _Code = value;
            }
        }

        /// <summary>
        /// Contenuto del payload quando la richiesta è andata bene.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public T PayloadOk { get; }

        /// <summary>
        /// Contenuto del payload quando la richiesta è andata male.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public E PayloadErr { get; }

        public Status(StatusCode code)
        {
            Code = code; PayloadOk = null; PayloadErr = null;
        }

        public Status(T payloadOk)
        {
            Code = StatusCode.Success; PayloadOk = payloadOk; PayloadErr = null;
        }

        public Status(E payloadErr)
        {
            Code = StatusCode.Error; PayloadErr = payloadErr; PayloadOk = null;
        }

        [JsonConstructor]
        public Status(StatusCode code, T payloadOk, E payloadErr)
        {
            Code = code; PayloadOk = payloadOk; PayloadErr = payloadErr;
        }
    }
}
