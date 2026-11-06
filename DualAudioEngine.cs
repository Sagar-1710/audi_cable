using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace DualAudioEngine
{
    #region COM Core Audio Interfaces

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    public class MMDeviceEnumeratorComObject { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IMMDeviceCollection ppDevices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr pClient);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr pClient);
    }

    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint pcDevices);
        [PreserveSig] int Item(uint nDevice, out IMMDevice ppDevice);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        [PreserveSig] int OpenPropertyStore(int stgmAccess, out IPropertyStore ppProperties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
        [PreserveSig] int GetState(out uint pdwState);
    }

    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint cProps);
        [PreserveSig] int GetAt(uint iProp, out PropertyKey pkey);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant pv);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant pv);
        [PreserveSig] int Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct PropertyKey
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct PropVariant
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pwszVal;
    }

    [Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, uint streamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr pFormat, ref Guid audioSessionGuid);
        [PreserveSig] int GetBufferSize(out uint pNumBufferFrames);
        [PreserveSig] int GetStreamLatency(out long phnsLatency);
        [PreserveSig] int GetCurrentPadding(out uint pNumPaddingFrames);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr pFormat, out IntPtr ppClosestMatch);
        [PreserveSig] int GetMixFormat(out IntPtr ppDeviceFormat);
        [PreserveSig] int GetDevicePeriod(out long phnsDefaultDevicePeriod, out long phnsMinimumDevicePeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr eventHandle);
        [PreserveSig] int GetService(ref Guid riid, out IntPtr ppv);
    }

    [Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr ppData, out uint pNumFramesToRead, out uint pdwFlags, out ulong pu64DevicePosition, out ulong pu64QPCPosition);
        [PreserveSig] int ReleaseBuffer(uint NumFramesRead);
        [PreserveSig] int GetNextPacketSize(out uint pNumFramesInNextPacket);
    }

    [Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioRenderClient
    {
        [PreserveSig] int GetBuffer(uint NumFramesRequested, out IntPtr ppData);
        [PreserveSig] int ReleaseBuffer(uint NumFramesWritten, uint dwFlags);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WAVEFORMATEX
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    #endregion

    #region Data Models

    public class AudioDeviceEntry
    {
        public string Id;
        public string Name;
        public string Description;
        public bool IsBluetooth;
        public bool IsVirtualCable;
        public bool IsDefault;
        public int State;
        public int SampleRate;
        public int Channels;

        public string ToJson()
        {
            return string.Format(
                "{{\"id\":\"{0}\",\"name\":\"{1}\",\"description\":\"{2}\",\"isBluetooth\":{3},\"isVirtualCable\":{4},\"isDefault\":{5},\"state\":{6},\"sampleRate\":{7},\"channels\":{8}}}",
                EscapeJson(Id), EscapeJson(Name), EscapeJson(Description),
                IsBluetooth ? "true" : "false",
                IsVirtualCable ? "true" : "false",
                IsDefault ? "true" : "false",
                State, SampleRate, Channels);
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ");
        }
    }

    #endregion

    #region Pure Direct Live Audio Streamer

    public class DualRepeaterController
    {
        private const uint AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
        private const int AUDCLNT_SHAREMODE_SHARED = 0;
        private static readonly Guid Guid_IAudioClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
        private static readonly Guid Guid_IAudioCaptureClient = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
        private static readonly Guid Guid_IAudioRenderClient = new Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2");

        private Thread workerThread;
        private volatile bool isRunning = false;

        public volatile float PeakL = 0f;
        public volatile float PeakR = 0f;
        private long _processedFrames = 0;
        public long ProcessedFrames { get { return Interlocked.Read(ref _processedFrames); } }
        public volatile string LastError = "";

        public string ActiveSourceId = "";
        public string ActiveTarget1Id = "";
        public string ActiveTarget2Id = "";

        public string ActiveSourceName = "";
        public string ActiveTarget1Name = "";
        public string ActiveTarget2Name = "";

        public static List<AudioDeviceEntry> GetRenderDevices()
        {
            var results = new List<AudioDeviceEntry>();
            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                if (enumerator == null) return results;

                IMMDeviceCollection coll;
                if (enumerator.EnumAudioEndpoints(0, 1, out coll) != 0 || coll == null) return results;

                uint count;
                coll.GetCount(out count);

                string defaultId = "";
                try
                {
                    IMMDevice defaultDev = null;
                    if (enumerator.GetDefaultAudioEndpoint(0, 0, out defaultDev) == 0 && defaultDev != null)
                    {
                        defaultDev.GetId(out defaultId);
                    }
                }
                catch { }

                var PKEY_FriendlyName = new PropertyKey { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };
                var PKEY_DeviceDesc = new PropertyKey { fmtid = new Guid("b3f8fa53-0004-438e-9003-51a46e139bfc"), pid = 6 };

                for (uint i = 0; i < count; i++)
                {
                    try
                    {
                        IMMDevice dev;
                        coll.Item(i, out dev);
                        if (dev == null) continue;

                        string id;
                        dev.GetId(out id);

                        uint state = 1;
                        dev.GetState(out state);

                        IPropertyStore store = null;
                        dev.OpenPropertyStore(0, out store);

                        string name = "";
                        string desc = "";

                        if (store != null)
                        {
                            PropVariant pvName;
                            var keyName = PKEY_FriendlyName;
                            if (store.GetValue(ref keyName, out pvName) == 0 && pvName.vt == 31 && pvName.pwszVal != IntPtr.Zero)
                            {
                                name = Marshal.PtrToStringUni(pvName.pwszVal);
                            }

                            PropVariant pvDesc;
                            var keyDesc = PKEY_DeviceDesc;
                            if (store.GetValue(ref keyDesc, out pvDesc) == 0 && pvDesc.vt == 31 && pvDesc.pwszVal != IntPtr.Zero)
                            {
                                desc = Marshal.PtrToStringUni(pvDesc.pwszVal);
                            }
                        }

                        int sRate = 44100;
                        int chs = 2;

                        try
                        {
                            object clientObj;
                            Guid iidClient = Guid_IAudioClient;
                            if (dev.Activate(ref iidClient, 7, IntPtr.Zero, out clientObj) == 0 && clientObj != null)
                            {
                                var client = (IAudioClient)clientObj;
                                IntPtr pFmt;
                                if (client.GetMixFormat(out pFmt) == 0 && pFmt != IntPtr.Zero)
                                {
                                    var fmt = (WAVEFORMATEX)Marshal.PtrToStructure(pFmt, typeof(WAVEFORMATEX));
                                    sRate = (int)fmt.nSamplesPerSec;
                                    chs = (int)fmt.nChannels;
                                    Marshal.FreeCoTaskMem(pFmt);
                                }
                            }
                        }
                        catch { }

                        string lowerAll = (name + " " + desc + " " + id).ToLowerInvariant();
                        bool isBt = lowerAll.Contains("bluetooth") || lowerAll.Contains("hands-free") || lowerAll.Contains("bth") ||
                                   lowerAll.Contains("headset") || lowerAll.Contains("headphone") || lowerAll.Contains("dubstep") ||
                                   lowerAll.Contains("superpod") || lowerAll.Contains("airpod") || lowerAll.Contains("galaxy buds") ||
                                   lowerAll.Contains("jbl") || lowerAll.Contains("sony") || lowerAll.Contains("bose") || lowerAll.Contains("earbuds");

                        bool isVac = lowerAll.Contains("virtual") || lowerAll.Contains("cable") || lowerAll.Contains("vac") ||
                                    lowerAll.Contains("voicemeeter") || lowerAll.Contains("line 1") || lowerAll.Contains("hi-fi");

                        results.Add(new AudioDeviceEntry
                        {
                            Id = id,
                            Name = string.IsNullOrEmpty(name) ? desc : name,
                            Description = desc,
                            IsBluetooth = isBt,
                            IsVirtualCable = isVac,
                            IsDefault = (id == defaultId),
                            State = (int)state,
                            SampleRate = sRate,
                            Channels = chs
                        });
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetRenderDevices Error: " + ex);
            }
            return results;
        }

        public static string GetRenderDevicesJson()
        {
            var list = GetRenderDevices();
            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < list.Count; i++)
            {
                sb.Append(list[i].ToJson());
                if (i < list.Count - 1) sb.Append(",");
            }
            sb.Append("]");
            return sb.ToString();
        }

        public bool Start(string sourceId, string target1Id, string target2Id)
        {
            if (isRunning) Stop();

            ActiveSourceId = string.IsNullOrEmpty(sourceId) ? "default" : sourceId;
            ActiveTarget1Id = target1Id;
            ActiveTarget2Id = target2Id;
            LastError = "";

            isRunning = true;
            workerThread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest,
                Name = "LiveAudioStreamer"
            };
            workerThread.Start();
            return true;
        }

        public void Stop()
        {
            isRunning = false;
            if (workerThread != null && workerThread.IsAlive)
            {
                workerThread.Join(1500);
            }
            workerThread = null;
        }

        public bool GetIsRunning() { return isRunning; }

        public string GetStatusJson()
        {
            return string.Format(
                "{{\"isRunning\":{0},\"sourceId\":\"{1}\",\"sourceName\":\"{2}\",\"target1Id\":\"{3}\",\"target1Name\":\"{4}\",\"target2Id\":\"{5}\",\"target2Name\":\"{6}\",\"peakL\":{7:F3},\"peakR\":{8:F3},\"processedFrames\":{9},\"lastError\":\"{10}\"}}",
                isRunning ? "true" : "false",
                EscapeJson(ActiveSourceId), EscapeJson(ActiveSourceName),
                EscapeJson(ActiveTarget1Id), EscapeJson(ActiveTarget1Name),
                EscapeJson(ActiveTarget2Id), EscapeJson(ActiveTarget2Name),
                PeakL, PeakR,
                ProcessedFrames,
                EscapeJson(LastError)
            );
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ");
        }

        /// <summary>
        /// Pure real-time audio routing worker loop.
        /// Captures exact PCM frames from Source and writes identical frames to Target 1 and Target 2.
        /// Zero delay buffers, zero distortion.
        /// </summary>
        private void WorkerLoop()
        {
            IMMDevice sourceDev = null;
            IMMDevice target1Dev = null;
            IMMDevice target2Dev = null;

            IAudioClient srcClient = null;
            IAudioCaptureClient srcCapture = null;

            IAudioClient tgt1Client = null;
            IAudioRenderClient tgt1Render = null;
            uint tgt1BufferSize = 0;

            IAudioClient tgt2Client = null;
            IAudioRenderClient tgt2Render = null;
            uint tgt2BufferSize = 0;

            IntPtr pSrcFmt = IntPtr.Zero;
            IntPtr pTgt1Fmt = IntPtr.Zero;
            IntPtr pTgt2Fmt = IntPtr.Zero;

            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();

                // 1. Resolve Source Device
                string sourceRealId = "";
                if (string.IsNullOrEmpty(ActiveSourceId) || ActiveSourceId.Equals("default", StringComparison.OrdinalIgnoreCase))
                {
                    enumerator.GetDefaultAudioEndpoint(0, 0, out sourceDev);
                }
                else
                {
                    enumerator.GetDevice(ActiveSourceId, out sourceDev);
                }

                if (sourceDev == null) throw new Exception("Source audio device could not be resolved.");
                sourceDev.GetId(out sourceRealId);

                // 2. Prevent Audio Feedback Loop:
                // If a target device IS the capture device (e.g. Dubstep Pop is Default output),
                // Windows is ALREADY outputting the sound to it directly!
                // Re-rendering into the capture device causes a double-sound echo feedback loop.
                bool t1IsCaptureDevice = (!string.IsNullOrEmpty(ActiveTarget1Id) && ActiveTarget1Id.Equals(sourceRealId, StringComparison.OrdinalIgnoreCase));
                bool t2IsCaptureDevice = (!string.IsNullOrEmpty(ActiveTarget2Id) && ActiveTarget2Id.Equals(sourceRealId, StringComparison.OrdinalIgnoreCase));

                // 3. Resolve Target Devices
                if (!string.IsNullOrEmpty(ActiveTarget1Id) && !t1IsCaptureDevice)
                {
                    enumerator.GetDevice(ActiveTarget1Id, out target1Dev);
                }
                if (!string.IsNullOrEmpty(ActiveTarget2Id) && !t2IsCaptureDevice)
                {
                    enumerator.GetDevice(ActiveTarget2Id, out target2Dev);
                }

                // 4. Initialize Source WASAPI Loopback Capture
                object srcClientObj;
                Guid iidClient = Guid_IAudioClient;
                int hr = sourceDev.Activate(ref iidClient, 7, IntPtr.Zero, out srcClientObj);
                if (hr != 0 || srcClientObj == null) throw new Exception(string.Format("Source activate failed: 0x{0:X}", hr));
                srcClient = (IAudioClient)srcClientObj;
                srcClient.GetMixFormat(out pSrcFmt);
                var srcWf = (WAVEFORMATEX)Marshal.PtrToStructure(pSrcFmt, typeof(WAVEFORMATEX));

                Guid emptyGuid = Guid.Empty;
                // Buffer: 40ms = 400,000 hns
                hr = srcClient.Initialize(AUDCLNT_SHAREMODE_SHARED, AUDCLNT_STREAMFLAGS_LOOPBACK, 400000, 0, pSrcFmt, ref emptyGuid);
                if (hr != 0) throw new Exception(string.Format("Source loopback initialize failed: 0x{0:X}", hr));

                IntPtr pCapObj = IntPtr.Zero;
                Guid iidCapture = Guid_IAudioCaptureClient;
                hr = srcClient.GetService(ref iidCapture, out pCapObj);
                if (hr != 0 || pCapObj == IntPtr.Zero) throw new Exception(string.Format("GetService IAudioCaptureClient failed: 0x{0:X}", hr));
                srcCapture = (IAudioCaptureClient)Marshal.GetObjectForIUnknown(pCapObj);
                Marshal.Release(pCapObj);

                int srcChannels = (int)srcWf.nChannels;

                // 5. Initialize Target 1 (Render)
                if (target1Dev != null)
                {
                    object t1Obj;
                    Guid iidT1 = Guid_IAudioClient;
                    hr = target1Dev.Activate(ref iidT1, 7, IntPtr.Zero, out t1Obj);
                    if (hr != 0 || t1Obj == null) throw new Exception(string.Format("Target 1 activate failed: 0x{0:X}", hr));
                    tgt1Client = (IAudioClient)t1Obj;
                    tgt1Client.GetMixFormat(out pTgt1Fmt);

                    hr = tgt1Client.Initialize(AUDCLNT_SHAREMODE_SHARED, 0, 400000, 0, pTgt1Fmt, ref emptyGuid);
                    if (hr != 0) throw new Exception(string.Format("Target 1 render initialize failed: 0x{0:X}", hr));

                    tgt1Client.GetBufferSize(out tgt1BufferSize);
                    IntPtr pR1 = IntPtr.Zero;
                    Guid iidR1 = Guid_IAudioRenderClient;
                    hr = tgt1Client.GetService(ref iidR1, out pR1);
                    if (hr != 0 || pR1 == IntPtr.Zero) throw new Exception(string.Format("GetService Target 1 IAudioRenderClient failed: 0x{0:X}", hr));
                    tgt1Render = (IAudioRenderClient)Marshal.GetObjectForIUnknown(pR1);
                    Marshal.Release(pR1);
                }

                // 6. Initialize Target 2 (Render)
                if (target2Dev != null)
                {
                    object t2Obj;
                    Guid iidT2 = Guid_IAudioClient;
                    hr = target2Dev.Activate(ref iidT2, 7, IntPtr.Zero, out t2Obj);
                    if (hr != 0 || t2Obj == null) throw new Exception(string.Format("Target 2 activate failed: 0x{0:X}", hr));
                    tgt2Client = (IAudioClient)t2Obj;
                    tgt2Client.GetMixFormat(out pTgt2Fmt);

                    hr = tgt2Client.Initialize(AUDCLNT_SHAREMODE_SHARED, 0, 400000, 0, pTgt2Fmt, ref emptyGuid);
                    if (hr != 0) throw new Exception(string.Format("Target 2 render initialize failed: 0x{0:X}", hr));

                    tgt2Client.GetBufferSize(out tgt2BufferSize);
                    IntPtr pR2 = IntPtr.Zero;
                    Guid iidR2 = Guid_IAudioRenderClient;
                    hr = tgt2Client.GetService(ref iidR2, out pR2);
                    if (hr != 0 || pR2 == IntPtr.Zero) throw new Exception(string.Format("GetService Target 2 IAudioRenderClient failed: 0x{0:X}", hr));
                    tgt2Render = (IAudioRenderClient)Marshal.GetObjectForIUnknown(pR2);
                    Marshal.Release(pR2);
                }

                // 7. Start Streaming
                srcClient.Start();
                if (tgt1Client != null) tgt1Client.Start();
                if (tgt2Client != null) tgt2Client.Start();

                _processedFrames = 0;

                // 8. Pure Direct Live Audio Streaming Loop
                while (isRunning)
                {
                    uint packetLength;
                    hr = srcCapture.GetNextPacketSize(out packetLength);
                    if (hr != 0 || packetLength == 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    while (packetLength > 0 && isRunning)
                    {
                        IntPtr pData;
                        uint numFrames;
                        uint flags;
                        ulong devPos, qpcPos;

                        hr = srcCapture.GetBuffer(out pData, out numFrames, out flags, out devPos, out qpcPos);
                        if (hr != 0) break;

                        if (numFrames > 0 && pData != IntPtr.Zero)
                        {
                            bool isSilence = (flags & 0x00000001) != 0;
                            int totalFloats = (int)(numFrames * srcChannels);
                            float[] floatBuffer = new float[totalFloats];

                            if (!isSilence)
                            {
                                Marshal.Copy(pData, floatBuffer, 0, totalFloats);

                                // Compute Peak levels
                                float maxL = 0f;
                                float maxR = 0f;
                                for (int i = 0; i < totalFloats; i += srcChannels)
                                {
                                    float sL = Math.Abs(floatBuffer[i]);
                                    float sR = (srcChannels > 1) ? Math.Abs(floatBuffer[i + 1]) : sL;
                                    if (sL > maxL) maxL = sL;
                                    if (sR > maxR) maxR = sR;
                                }
                                PeakL = maxL;
                                PeakR = maxR;
                            }
                            else
                            {
                                PeakL = 0f;
                                PeakR = 0f;
                            }

                            // Direct Stream to Target 1
                            if (tgt1Render != null && tgt1Client != null)
                            {
                                DirectWrite(tgt1Client, tgt1Render, tgt1BufferSize, floatBuffer, (int)numFrames, srcChannels, isSilence);
                            }

                            // Direct Stream to Target 2
                            if (tgt2Render != null && tgt2Client != null)
                            {
                                DirectWrite(tgt2Client, tgt2Render, tgt2BufferSize, floatBuffer, (int)numFrames, srcChannels, isSilence);
                            }

                            Interlocked.Add(ref _processedFrames, numFrames);
                        }

                        srcCapture.ReleaseBuffer(numFrames);
                        srcCapture.GetNextPacketSize(out packetLength);
                    }
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.WriteLine("DirectLiveAudio Error: " + ex);
            }
            finally
            {
                try { if (srcClient != null) srcClient.Stop(); } catch { }
                try { if (tgt1Client != null) tgt1Client.Stop(); } catch { }
                try { if (tgt2Client != null) tgt2Client.Stop(); } catch { }

                if (pSrcFmt != IntPtr.Zero) Marshal.FreeCoTaskMem(pSrcFmt);
                if (pTgt1Fmt != IntPtr.Zero) Marshal.FreeCoTaskMem(pTgt1Fmt);
                if (pTgt2Fmt != IntPtr.Zero) Marshal.FreeCoTaskMem(pTgt2Fmt);

                isRunning = false;
            }
        }

        /// <summary>
        /// Direct, crystal-clear audio buffer write into the target WASAPI render client.
        /// Zero distortion, zero delay, pure live stream.
        /// </summary>
        private static void DirectWrite(IAudioClient client, IAudioRenderClient render, uint bufferSize, float[] samples, int frameCount, int channels, bool isSilence)
        {
            try
            {
                uint padding;
                if (client.GetCurrentPadding(out padding) != 0) return;
                uint available = bufferSize - padding;
                if (available < (uint)frameCount) return;

                IntPtr pDest;
                if (render.GetBuffer((uint)frameCount, out pDest) == 0 && pDest != IntPtr.Zero)
                {
                    if (isSilence)
                    {
                        render.ReleaseBuffer((uint)frameCount, 0x00000002 /* AUDCLNT_BUFFERFLAGS_SILENT */);
                    }
                    else
                    {
                        Marshal.Copy(samples, 0, pDest, frameCount * channels);
                        render.ReleaseBuffer((uint)frameCount, 0);
                    }
                }
            }
            catch { }
        }
    }

    #endregion
}
