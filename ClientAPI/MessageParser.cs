using System;
using System.Collections.Generic;
using System.Text;

namespace ClientAPI
{
    public class MessageParser
    {
        public enum States
        {
            Empty,
            Listening,
            End
        }

        States currentState = States.Empty;
        int messageLength = 0;
        int messageLengthHeaderSize = 0;
        List<byte[]> messageHolder = new List<byte[]>();

        //parses a message
        //the message should be in the following format
        // "<" + length of payload + ">" + an object of type Message
        //returns (T) Message - returns the actual message object that was sent
        //returns (T) Empty Message - returns
        public byte[] ReceiverParser(byte[] byteArray, int bytesReceived)
        {
            //if the parser hasn't started parsing anything or we still don't know the length of the payload
            //so we keep adding the byteArray to our list and then check if we have a valid header for length
            if (currentState == States.Empty)
            {
                byte[] b = new byte[bytesReceived];

                Buffer.BlockCopy(byteArray, 0, b, 0, bytesReceived);

                messageHolder.Add(b);

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
            }

            //we know the length of the packet if we are in the listening state
            if (currentState == States.Listening)
            {
                byte[] wholeMessage = CompleteByteArray();
                byte[] message = new byte[wholeMessage.Length];

                //check if we have the whole packet as specified by the length header
                if (wholeMessage.Length - messageLengthHeaderSize == messageLength)
                {
                    Buffer.BlockCopy(wholeMessage, messageLengthHeaderSize, message, 0, messageLength);

                    currentState = States.Empty;
                    messageLength = 0;
                    messageLengthHeaderSize = 0;
                    messageHolder.RemoveRange(0, messageHolder.Count);

                    return message;
                }
            }

            return null;
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
