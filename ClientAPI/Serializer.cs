using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace ClientAPI
{
    public class Serializer<T>
    {
        public static byte[] Serialize(T message)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryFormatter bf = new BinaryFormatter();

                    bf.Serialize(ms, message);

                    return ms.ToArray();
                }
            }
            catch (SerializationException)
            {
                throw;
            }
        }

        public static T Deserialize(byte[] data)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryFormatter bf = new BinaryFormatter();

                    return (T)bf.Deserialize(ms);
                }
            }
            catch (SerializationException)
            {
                throw;
            }
        }
    }
}
