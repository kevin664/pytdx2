using System;
using System.IO;

namespace Tdx.Core.Parsers
{
    /// <summary>
    /// Base class for all TDX protocol parsers.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response object this parser will produce.</typeparam>
    public abstract class BaseParser<TResponse>
    {
        /// <summary>
        /// The message ID for this specific request.
        /// </summary>
        public ushort MsgId { get; protected set; }

        /// <summary>
        /// The payload of the request.
        /// </summary>
        protected byte[] Body { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Serializes the entire request packet, including the header and body.
        /// </summary>
        /// <returns>A byte array representing the full request packet.</returns>
        public byte[] Serialize()
        {
            using var stream = new MemoryStream();
            // Use a BinaryWriter to write data in binary format.
            // The 'true' argument for leaveOpen is not needed as we control the stream's lifecycle here.
            using var writer = new BinaryWriter(stream);

            // The Python code's `body` variable includes the msg_id (2 bytes).
            // So its length is 2 + payload_length.
            ushort pythonBodyLength = (ushort)(2 + this.Body.Length);

            // --- Write Header ---
            // The header format is equivalent to Python's struct.pack('<BIBHHH', ...), a 12-byte header.
            writer.Write((byte)0x0c);       // Zipped: 0x0c indicates no compression.
            writer.Write((uint)0);          // Customize: Not used, set to 0.
            writer.Write((byte)1);          // Control: Set to 1.
            writer.Write(pythonBodyLength); // ZipSize: The size of the body including the 2-byte MsgId.
            writer.Write(pythonBodyLength); // UnzipSize: Same as ZipSize when not compressed.

            // --- Write Body ---
            // The body of the packet starts with the message ID.
            writer.Write(this.MsgId);
            // Followed by the actual payload for the request.
            writer.Write(this.Body);

            return stream.ToArray();
        }

        /// <summary>
        /// Deserializes the response byte array into a strongly-typed response object.
        /// To be implemented by derived parser classes.
        /// </summary>
        /// <param name="responseBody">The byte array received from the server.</param>
        /// <returns>A strongly-typed response object.</returns>
        public abstract TResponse Deserialize(byte[] responseBody);
    }
}
