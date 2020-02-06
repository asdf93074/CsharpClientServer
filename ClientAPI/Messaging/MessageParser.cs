using System;
using System.Collections.Generic;
using System.Text;

namespace ClientAPI.Messaging
{
    public class MessageParser
    {
        public enum States
        {
            Empty,
            Listening
        }

        public States currentState = States.Empty;
        States prevState = States.Empty;
        int messageLength = 0;
        int messageLengthHeaderSize = 0;
        public List<byte[]> messageHolder = new List<byte[]>();
        Queue<byte[]> incomingMessageQueue = new Queue<byte[]>();

        //parses a message
        //the message should be in the following format
        // "<" + length of payload + ">" + an object of type Message
        //returns (T) Message - returns the actual message object that was sent
        //returns (T) Empty Message - returns
        public Queue<byte[]> ReceiverParser(byte[] byteArray, int bytesReceived)
        {
            incomingMessageQueue.Clear();
            while (true)
            {
                bool morePackets = false;

                byte[] b = new byte[bytesReceived];

                Buffer.BlockCopy(byteArray, 0, b, 0, bytesReceived);

                messageHolder.Add(b);

                //if the parser hasn't started parsing anything or we still don't know the length of the payload
                //so we keep adding the byteArray to our list and then check if we have a valid header for length
                if (currentState == States.Empty)
                {
                    string payloadLength = null;
                    string completeMessage = Encoding.ASCII.GetString(CompleteByteArray());
                    //check for the starting of the length header "<"
                    if (completeMessage.IndexOf("<") != -1)
                    {
                        //check for the ending of the length header ">"
                        if (completeMessage.IndexOf(">") != -1)
                        {
                            for (int i = completeMessage.IndexOf("<") + 1; i < completeMessage.IndexOf(">"); i++)
                            {
                                payloadLength += completeMessage[i];
                            }

                            //we know the length now we know how much to read in the next packets we get
                            messageLength = Int32.Parse(payloadLength);
                            messageLengthHeaderSize = Encoding.ASCII.GetBytes("<" + messageLength.ToString() + ">").Length;
                            currentState = States.Listening;
                        }
                    }

                    // not required since default value is false
                    if (currentState != States.Listening)
                    {
                        morePackets = false;
                    }
                }

                //we know the length of the packet if we are in the listening state
                if (currentState == States.Listening)
                {
                    byte[] wholeMessage = CompleteByteArray();
                    byte[] message = new byte[messageLength];

                    //check if we have the whole packet as specified by the length header
                    if (messageLength + messageLengthHeaderSize <= wholeMessage.Length)
                    {
                        //if the total message size is more than what our header tells us
                        //then go back and check for new messages
                        if (messageLength + messageLengthHeaderSize < wholeMessage.Length)
                        {
                            morePackets = true;
                        }

                        Buffer.BlockCopy(wholeMessage, messageLengthHeaderSize, message, 0, messageLength);

                        currentState = States.Empty;

                        incomingMessageQueue.Enqueue(message);

                        //if there are more packets
                        //then shift the bytes after our current packet byte array to the beginning of the byte array
                        //and set the rest of the elements to 0 byte
                        //also change the receivedBytes value to totalBytes - bytesInCurrentPacket
                        if (morePackets)
                        {
                            Console.WriteLine("MORE PACKETS {0} {1} {2}", wholeMessage.Length, messageLength,
                                messageLengthHeaderSize);
                            for (int i = 0; i < byteArray.Length; i++)
                            {
                                int offset = i + messageLength + messageLengthHeaderSize;
                                if (offset < wholeMessage.Length)
                                {
                                    byteArray[i] = wholeMessage[offset];
                                }
                                else
                                {
                                    byteArray[i] = (byte)0;
                                }
                            }

                            bytesReceived = wholeMessage.Length - messageLength - messageLengthHeaderSize;
                        }

                        // clearing for next packet
                        messageLength = 0;
                        messageLengthHeaderSize = 0;
                        messageHolder.RemoveRange(0, messageHolder.Count);
                        Console.WriteLine(messageHolder.Count.ToString());
                    }
                }

                if (!morePackets)
                {
                    return incomingMessageQueue;
                }
            }
        }

        //joins all the byte arrays stored in our messageHolder together to check if we have anything valid to change the parser's state
        public byte[] CompleteByteArray()
        {
            int totalLength = 0;

            foreach (var b in messageHolder)
            {
                totalLength += b.Length;
            }

            byte[] completeArray = new byte[totalLength];

            int prevLength = 0;
            foreach (var b in messageHolder)
            {
                Buffer.BlockCopy(b, 0, completeArray, prevLength, b.Length);
                prevLength = b.Length;
            }

            return completeArray;
        }

        public byte[] SenderParser(byte[] data)
        {
            byte[] lengthHeader = Encoding.ASCII.GetBytes("<" + data.Length.ToString() + ">");
            byte[] packet = new byte[lengthHeader.Length + data.Length];

            Buffer.BlockCopy(lengthHeader, 0, packet, 0, lengthHeader.Length);
            Buffer.BlockCopy(data, 0, packet, lengthHeader.Length, data.Length);

            return packet;
        }
    }
}
