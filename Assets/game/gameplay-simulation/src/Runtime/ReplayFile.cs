using System;
using System.IO;
using Testability;

namespace GameplaySimulation
{
    public static class ReplayFile
    {
        public static void SaveNew(string path, ReplayArtifact artifact)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            artifact.Validate();
            // Serialize before opening the destination: validation/serialization failures leave no partial file.
            using (MemoryStream bytes = new MemoryStream())
            {
                ArtifactJson.Write(bytes, artifact);
                if (bytes.Length > 32 * 1024 * 1024) throw new ArgumentException("Replay exceeds 32 MiB.");
                bytes.Position = 0;
                using (FileStream output = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) bytes.CopyTo(output);
            }
        }
        public static ReplayArtifact Load(string path)
        {
            using (FileStream input = File.OpenRead(path))
            {
                if (input.Length > 32 * 1024 * 1024) throw new ArgumentException("Replay exceeds 32 MiB.");
                ReplayArtifact artifact = ArtifactJson.Read<ReplayArtifact>(input);
                if (artifact == null) throw new ArgumentException("Missing replay.");
                artifact.Validate(); return artifact;
            }
        }
    }
}
