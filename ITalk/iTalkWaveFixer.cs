using System.IO;
using UnityEngine;

public static class iTalkWaveFixer
{
    // wav byte[] 입력, 헤더 교정 후 byte[] 반환
    public static byte[] FixWavHeader(byte[] wavData)
    {
        using (MemoryStream ms = new MemoryStream(wavData))
        using (BinaryReader reader = new BinaryReader(ms))
        using (MemoryStream outMs = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(outMs))
        {
            // 1. RIFF 헤더 복사
            writer.Write(reader.ReadBytes(4)); // "RIFF"

            // 2. (임시) RIFF chunk size 복사
            writer.Write(reader.ReadBytes(4)); // 나중에 수정

            writer.Write(reader.ReadBytes(4)); // "WAVE"

            // 3. fmt, data 등 chunk 전체 복사
            byte[] rest = reader.ReadBytes((int)(ms.Length - ms.Position));
            writer.Write(rest);

            // 4. data chunk 찾기 (offset 계산)
            int riffChunkSize = (int)(outMs.Length - 8);
            int dataChunkPos = 12;
            while (dataChunkPos < outMs.Length - 8)
            {
                outMs.Position = dataChunkPos;
                byte[] chunkId = new byte[4];
                outMs.Read(chunkId, 0, 4);
                int chunkSize = outMs.ReadByte() | (outMs.ReadByte() << 8) | (outMs.ReadByte() << 16) | (outMs.ReadByte() << 24);
                if (System.Text.Encoding.ASCII.GetString(chunkId) == "data")
                {
                    // data chunk size 고치기
                    int trueDataSize = (int)(outMs.Length - dataChunkPos - 8);
                    outMs.Position = dataChunkPos + 4;
                    outMs.WriteByte((byte)(trueDataSize & 0xFF));
                    outMs.WriteByte((byte)((trueDataSize >> 8) & 0xFF));
                    outMs.WriteByte((byte)((trueDataSize >> 16) & 0xFF));
                    outMs.WriteByte((byte)((trueDataSize >> 24) & 0xFF));
                    break;
                }
                dataChunkPos += 8 + chunkSize;
            }
            // 5. RIFF chunk size 고치기
            outMs.Position = 4;
            outMs.WriteByte((byte)(riffChunkSize & 0xFF));
            outMs.WriteByte((byte)((riffChunkSize >> 8) & 0xFF));
            outMs.WriteByte((byte)((riffChunkSize >> 16) & 0xFF));
            outMs.WriteByte((byte)((riffChunkSize >> 24) & 0xFF));

            // 6. 결과 반환
            return outMs.ToArray();
        }
    }
}