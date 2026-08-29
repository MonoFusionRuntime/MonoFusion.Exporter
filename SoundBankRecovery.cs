using System.Text;

namespace MonoFusion.Exporter
{
    public class SoundBankRecovery
    {
        private const ushort CHUNK_SOUNDBANK = 0x6668;

        private class SoundBankItem
        {
            public uint handle;
            public uint flags;
            public string name = string.Empty;
        }

        private SoundBankItem[] Items = [];

        private SoundBankRecovery()
        {

        }

        public static SoundBankRecovery ReadFromMFA(string mfaPath)
        {
            SoundBankRecovery soundBank = new SoundBankRecovery();
            BinaryReader mfaReader = new BinaryReader(File.OpenRead(mfaPath));
            mfaReader.BaseStream.Position = 20;
            for (int i = 0; i < 3; i++)
            {
                ushort skip = mfaReader.ReadUInt16();
                mfaReader.BaseStream.Position += skip * 2 + 2;
            }
            uint stampSize = mfaReader.ReadUInt32();
            mfaReader.BaseStream.Position += stampSize + 4; // 4 = ATNF
            uint fontCount = mfaReader.ReadUInt32();
            mfaReader.BaseStream.Position += fontCount * 108; // 108 = Font Size

            mfaReader.BaseStream.Position += 4; // 4 = APMS
            uint soundCount = mfaReader.ReadUInt32();
            soundBank.Items = new SoundBankItem[soundCount];
            for (int i = 0; i < soundCount; i++)
            {
                SoundBankItem item = new SoundBankItem();
                item.handle = mfaReader.ReadUInt32();
                mfaReader.BaseStream.Position += 8;
                uint size = mfaReader.ReadUInt32();
                item.flags = mfaReader.ReadUInt32();
                mfaReader.BaseStream.Position += 4;
                int nameLength = mfaReader.ReadInt32();
                item.name = Encoding.Unicode.GetString(mfaReader.ReadBytes(nameLength * 2)).TrimEnd('\0');
                mfaReader.BaseStream.Position += size - nameLength * 2;
                soundBank.Items[i] = item;

                Console.WriteLine($"Added Sound ({item.handle}) {item.name} with flags {item.flags}");
            }
            mfaReader.Close();
            return soundBank;
        }

        public void WriteToCCN(CCNFeeder ccnFeeder, Dictionary<uint, int> frequencies)
        {
            BinaryWriter data = new BinaryWriter(new MemoryStream());
            data.Write(Items.Length);
            foreach (SoundBankItem item in Items)
            {
                data.Write((ushort)item.handle);
                data.Write((ushort)item.flags);
                data.Write(frequencies[item.handle]);
                data.Write((ushort)item.name.Length);
                data.Write(Encoding.Unicode.GetBytes(item.name));
            }

            ccnFeeder.ReplaceChunk(CHUNK_SOUNDBANK, (MemoryStream)data.BaseStream);
            data.Close();
        }
    }
}
