using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using ClientAPI.Messaging;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace ClientAPI.Messaging.Tests
{
    [TestClass()]
    public class MessageParserTests
    {
        [TestMethod()]
        public void ReceiverParserTest_ValidPacket()
        {
            // arrange
            MessageParser parser = new MessageParser();
            Message message = new Message {
                SenderClientID = "A",
                ReceiverClientID = "B",
                MessageBody = "Hello",
                MessageType = MessageType.ClientMessage,
                Broadcast = false
            };
            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();

                bf.Serialize(ms, message);

                data = ms.ToArray();
            }
            string lengthHeader = "<" + data.Length.ToString() + ">";
            byte[] packet = parser.SenderParser(data);
            int bytesReceived = data.Length + Encoding.ASCII.GetBytes(lengthHeader).Length;

            // act
            Queue<byte[]> messages = parser.ReceiverParser(packet, bytesReceived);

            // assert
            foreach (byte[] b in messages)
            {
                Message receivedMessage = Serializer<Message>.Deserialize(b);
                Assert.AreEqual(message.SenderClientID, receivedMessage.SenderClientID);
                Assert.AreEqual(message.ReceiverClientID, receivedMessage.ReceiverClientID);
                Assert.AreEqual(message.MessageBody, receivedMessage.MessageBody);
                Assert.AreEqual(message.MessageType, receivedMessage.MessageType);
                Assert.AreEqual(message.Broadcast, receivedMessage.Broadcast);
            }
        }

        [TestMethod()]
        public void ReceiverParserTest_PartialPacket()
        {
            // arrange
            MessageParser parser = new MessageParser();
            Message message = new Message
            {
                SenderClientID = "A",
                ReceiverClientID = "B",
                MessageBody = "Hello",
                MessageType = MessageType.ClientMessage,
                Broadcast = false
            };
            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();

                bf.Serialize(ms, message);

                data = ms.ToArray();
            }
            byte[] malformedData = new byte[data.Length / 2];
            Buffer.BlockCopy(data, 0, malformedData, 0, data.Length / 2);
            string lengthHeader = "<" + malformedData.Length.ToString() + ">";
            byte[] packet = parser.SenderParser(malformedData);
            int bytesReceived = malformedData.Length/2 + Encoding.ASCII.GetBytes(lengthHeader).Length;

            // act
            Queue<byte[]> messages = parser.ReceiverParser(packet, bytesReceived);

            // assert
            if (messages.Count > 0)
            {
                Assert.Fail("It formed a message from an incomplete message packet.");
            }
        }

        [TestMethod()]
        public void ReceiverParserTest_MultiplePartialPackets()
        {
            // arrange
            MessageParser parser = new MessageParser();
            Message message = new Message
            {
                SenderClientID = "A",
                ReceiverClientID = "B",
                MessageBody = "Hellos",
                MessageType = MessageType.ClientMessage,
                Broadcast = false
            };
            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();

                bf.Serialize(ms, message);

                data = ms.ToArray();
            }

            string lengthHeader = "<" + data.Length.ToString() + ">";
            byte[] lengthHeaderPacket = Encoding.ASCII.GetBytes(lengthHeader);
            
            int bytesReceived = Encoding.ASCII.GetBytes(lengthHeader).Length;

            // act
            Queue<byte[]> messages = parser.ReceiverParser(lengthHeaderPacket, bytesReceived);

            // assert
            if (messages.Count > 0)
            {
                Assert.Fail("It formed a message from an incomplete message packet.");
            }

            // act
            messages = parser.ReceiverParser(data, data.Length);

            // assert
            if (messages.Count == 0)
            {
                Assert.Fail("It failed to form a message from two partial packets.");
            }

            // assert
            foreach (byte[] b in messages)
            {
                Message receivedMessage = Serializer<Message>.Deserialize(b);
                Assert.AreEqual(message.SenderClientID, receivedMessage.SenderClientID);
                Assert.AreEqual(message.ReceiverClientID, receivedMessage.ReceiverClientID);
                Assert.AreEqual(message.MessageBody, receivedMessage.MessageBody);
                Assert.AreEqual(message.MessageType, receivedMessage.MessageType);
                Assert.AreEqual(message.Broadcast, receivedMessage.Broadcast);
            }
        }

        [TestMethod()]
        public void CompleteByteArrayTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void SenderParserTest()
        {
            Assert.Fail();
        }
    }
}