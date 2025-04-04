using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Dsp;
using NAudio.Wave;
using FirstResponseGPT.Core;

namespace FirstResponseGPT.Utils
{
    public static class AudioUtils
    {

        /// <summary>
        /// Checks if the provided audio data is valid
        /// </summary>
        public static bool IsValidAudioData(byte[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
                return false;

            try
            {
                using (var ms = new MemoryStream(audioData))
                using (var reader = new Mp3FileReader(ms))
                {
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static TimeSpan? GetAudioDuration(byte[] audioData)
        {
            try
            {
                using (var ms = new MemoryStream(audioData))
                using (var reader = new Mp3FileReader(ms))
                {
                    return reader.TotalTime;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("AudioUtils", "Error getting audio duration.", ex);
                return null;
            }
        }

        /// <summary>
        /// Adjusts the volume of the provided audio data
        /// </summary>
        // Utils/AudioUtils.cs
        public static byte[] AdjustVolume(byte[] audioData, float volumeMultiplier)
        {
            try
            {
                MemoryStream inputStream = null;
                Mp3FileReader reader = null;
                MemoryStream outputStream = null;
                WaveFileWriter writer = null;

                try
                {
                    inputStream = new MemoryStream(audioData);
                    reader = new Mp3FileReader(inputStream);
                    outputStream = new MemoryStream();
                    writer = new WaveFileWriter(outputStream, reader.WaveFormat);

                    var buffer = new byte[4096];
                    int bytesRead;

                    while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        for (int i = 0; i < bytesRead; i += 2)
                        {
                            var sample = BitConverter.ToInt16(buffer, i);
                            sample = (short)(sample * volumeMultiplier);
                            var bytes = BitConverter.GetBytes(sample);
                            writer.Write(bytes, 0, bytes.Length);
                        }
                    }

                    writer.Flush();
                    return outputStream.ToArray();
                }
                finally
                {
                    writer?.Dispose();
                    outputStream?.Dispose();
                    reader?.Dispose();
                    inputStream?.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("AudioUtils", "Error adjusting audio volume.", ex);
                return audioData;
            }
        }
    }
}