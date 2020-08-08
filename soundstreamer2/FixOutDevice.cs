using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NAudio;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace soundstreamer2
{
    class FixOutDevice : IDisposable
    {
        public FixOutDevice(WaveFormat waveFormat)
        {
            this.waveOut = new WaveOut();
            this.waveFormat = waveFormat;
            this.bufferedWave = new BufferedWaveProvider(waveFormat) { DiscardOnBufferOverflow = true, BufferDuration = TimeSpan.FromSeconds(3) };
        }
        private float _volume = .2f;
        public float Volume
        {
            get => _volume;
            set
            {
                _volume = value;
                if (waveOut != null) waveOut.Volume = value;
            }
        }
        private WaveOut waveOut;
        public BufferedWaveProvider bufferedWave;
        private readonly WaveFormat waveFormat;
        private MMDeviceEnumerator deviceEnum = new MMDeviceEnumerator();
        private NotificationClientImplementation notificationClient;
        private NAudio.CoreAudioApi.Interfaces.IMMNotificationClient notifyClient;
        public void Init()
        {
            RestartOut();
            notificationClient = new NotificationClientImplementation(this);
            notifyClient = notificationClient;
            deviceEnum.RegisterEndpointNotificationCallback(notifyClient);
        }
        public void RestartOut()
        {
            waveOut?.Stop();
            waveOut?.Dispose();
            waveOut = new WaveOut();
            //waveOut.NumberOfBuffers = 3;
            //waveOut.DesiredLatency = 50 * waveOut.NumberOfBuffers;
            bufferedWave = new BufferedWaveProvider(waveFormat);
            waveOut.Init(bufferedWave);
            waveOut.Volume = Volume;
            waveOut.Play();
        }

        public void Dispose()
        {
            waveOut?.Dispose();
            deviceEnum?.UnregisterEndpointNotificationCallback(notifyClient);
            deviceEnum?.Dispose();
        }
    }
    class NotificationClientImplementation : NAudio.CoreAudioApi.Interfaces.IMMNotificationClient
    {
        private readonly FixOutDevice fod;
        public void OnDefaultDeviceChanged(DataFlow dataFlow, Role deviceRole, string defaultDeviceId)
        {
            //Do some Work
            //Console.WriteLine("OnDefaultDeviceChanged --> {0}", dataFlow.ToString());

            //Console.WriteLine("defaultDeviceId=" + defaultDeviceId);
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                fod.RestartOut();//try doing this async next
            });
        }

        public void OnDeviceAdded(string deviceId)
        {
            //Do some Work
            //Console.WriteLine("OnDeviceAdded -->");
        }

        public void OnDeviceRemoved(string deviceId)
        {
            //Console.WriteLine("OnDeviceRemoved -->");
            //Do some Work
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
            //Console.WriteLine("OnDeviceStateChanged\n Device Id -->{0} : Device State {1}", deviceId, newState);
            //Do some Work
        }

        public NotificationClientImplementation(FixOutDevice fod)
        {
            this.fod = fod;
            //_realEnumerator.RegisterEndpointNotificationCallback();
            if (System.Environment.OSVersion.Version.Major < 6)
            {
                throw new NotSupportedException("This functionality is only supported on Windows Vista or newer.");
            }
        }

        public void OnPropertyValueChanged(string deviceId, PropertyKey propertyKey)
        {
            //Do some Work
            //fmtid & pid are changed to formatId and propertyId in the latest version NAudio
            //Console.WriteLine("OnPropertyValueChanged: formatId --> {0}  propertyId --> {1}", propertyKey.formatId.ToString(), propertyKey.propertyId.ToString());
        }
    }
}
