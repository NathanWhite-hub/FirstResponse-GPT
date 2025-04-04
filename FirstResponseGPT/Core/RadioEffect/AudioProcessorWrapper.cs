using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FirstResponseGPT.Core.RadioEffect
{
    public class AudioProcessorWrapper : IDisposable
    {
        private IntPtr _processorPtr;
        private bool _disposed;
        private const string DllPath = "plugins/LSPDFR/FirstResponseGPT/lib/FirstResponseGPTRadioProcessor.dll";

        // Import native functions
        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr CreateProcessor(double sampleRate, int maxBlockSize);

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ProcessBlock(IntPtr processor, float[] data, int numSamples);

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void AddEQBand(IntPtr processor,
            float frequency,
            float gain,
            float q,
            int filterType,
            bool enabled,
            int slope);

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ClearEQBands(IntPtr processor);

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetEQSettings(IntPtr processor,
            float outputGain,
            bool analyzerEnabled,
            int quality);

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetCompressorParams(IntPtr processor,
            float threshold,
            float ratio,
            float attackTime,
            float releaseTime,
            float kneeWidth,
            float makeupGain,
            int style,
            bool autoGain,
            float dryMix);

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern float GetCompressorGainReduction(IntPtr processor);

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetSaturatorParams(IntPtr processor,
            float drive,
            float lowCut,
            float highCut,
            float tone,
            float mix,
            bool punishMode,
            int style,
            float output,
            bool oversampling,
            float staticVolume);

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetProcessingChain(IntPtr processor,
            bool eqEnabled,
            bool compEnabled,
            bool satEnabled,
            int[] processingOrder);

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetProcessorError();

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void GetProcessorErrorString(
            [MarshalAs(UnmanagedType.LPStr)] StringBuilder buffer,
            int bufferSize);

        [DllImport(DllPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void DestroyProcessor(IntPtr processor);

        // Enums matching the C++ side
        public enum FilterType
        {
            Bell,
            LowShelf,
            HighShelf,
            LowCut,
            HighCut,
            Notch,
            BandPass
        }

        public enum CompStyle
        {
            Clean,
            Classic,
            Opto,
            Punch
        }

        public enum SatStyle
        {
            A,  // Ampex
            E,  // EMI
            N,  // Neve
            T,  // Tube
            P   // Plate
        }

        // Constructor
        public AudioProcessorWrapper(double sampleRate, int maxBlockSize = 1024)
        {
            _processorPtr = CreateProcessor(sampleRate, maxBlockSize);
            if (_processorPtr == IntPtr.Zero)
            {
                var error = GetLastErrorMessage();
                throw new InvalidOperationException($"Failed to create audio processor: {error}");
            }
        }

        // Main processing method
        public void ProcessAudio(float[] buffer)
        {
            ThrowIfDisposed();
            ProcessBlock(_processorPtr, buffer, buffer.Length);
            CheckForErrors();
        }

        // EQ Methods
        public void AddEQBand(float frequency, float gain, float q, FilterType type, bool enabled = true, int slope = 12)
        {
            ThrowIfDisposed();
            AddEQBand(_processorPtr, frequency, gain, q, (int)type, enabled, slope);
            CheckForErrors();
        }

        public void ClearEQ()
        {
            ThrowIfDisposed();
            ClearEQBands(_processorPtr);
            CheckForErrors();
        }

        public void ConfigureEQ(float outputGain = 0.0f, bool analyzerEnabled = true, int quality = 1)
        {
            ThrowIfDisposed();
            SetEQSettings(_processorPtr, outputGain, analyzerEnabled, quality);
            CheckForErrors();
        }

        // Compressor Methods
        public void SetCompressor(
            float threshold = -20.0f,
            float ratio = 2.0f,
            float attackTime = 10.0f,
            float releaseTime = 100.0f,
            float kneeWidth = 6.0f,
            float makeupGain = 0.0f,
            CompStyle style = CompStyle.Clean,
            bool autoGain = true,
            float dryMix = 0.0f)
        {
            ThrowIfDisposed();
            SetCompressorParams(_processorPtr, threshold, ratio, attackTime, releaseTime,
                kneeWidth, makeupGain, (int)style, autoGain, dryMix);
            CheckForErrors();
        }

        public float GetCompressorReduction()
        {
            ThrowIfDisposed();
            return GetCompressorGainReduction(_processorPtr);
        }

        // Saturator Methods
        public void SetSaturator(
            float drive = 3.0f,
            float lowCut = 20.0f,
            float highCut = 20000.0f,
            float tone = 0.0f,
            float mix = 100.0f,
            bool punishMode = false,
            SatStyle style = SatStyle.A,
            float output = 0.0f,
            bool oversampling = true,
            float staticVolume = 0.0f)  // Add the new parameter with default value
        {
            ThrowIfDisposed();
            SetSaturatorParams(_processorPtr, drive, lowCut, highCut, tone, mix,
                punishMode, (int)style, output, oversampling, staticVolume);  // Add staticVolume to the call
            CheckForErrors();
        }

        // Chain Control
        public void ConfigureChain(bool eqEnabled, bool compEnabled, bool satEnabled, int[] order)
        {
            ThrowIfDisposed();
            if (order == null || order.Length != 3)
                throw new ArgumentException("Processing order must be an array of length 3");

            SetProcessingChain(_processorPtr, eqEnabled, compEnabled, satEnabled, order);
            CheckForErrors();
        }

        // Error handling
        private string GetLastErrorMessage()
        {
            var buffer = new StringBuilder(1024);
            GetProcessorErrorString(buffer, buffer.Capacity);
            return buffer.ToString();
        }

        private void CheckForErrors()
        {
            var errorCode = GetProcessorError();
            if (errorCode != 0)
            {
                var message = GetLastErrorMessage();
                throw new InvalidOperationException($"Processor error {errorCode}: {message}");
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AudioProcessorWrapper));
        }

        // IDisposable implementation
        public void Dispose()
        {
            if (!_disposed)
            {
                if (_processorPtr != IntPtr.Zero)
                {
                    DestroyProcessor(_processorPtr);
                    _processorPtr = IntPtr.Zero;
                }
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        ~AudioProcessorWrapper()
        {
            Dispose();
        }
    }
}
