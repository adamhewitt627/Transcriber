using Microsoft.Azure.WebJobs;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Transcriber
{
    public class Recognize
    {
        private readonly SpeechConfig _speechConfig;

        public Recognize(SpeechConfig speechConfig)
        {
            _speechConfig = speechConfig ?? throw new ArgumentNullException(nameof(speechConfig));
        }


        [FunctionName("Function1")]
        public async Task Run([BlobTrigger("audio-workitems/{name}")]CloudBlockBlob myBlob, string name, ILogger log)
        {
            if (myBlob is null) throw new ArgumentNullException(nameof(myBlob));
            if (name is null) throw new ArgumentNullException(nameof(name));
            if (log is null) throw new ArgumentNullException(nameof(log));


            BlobProperties properties = myBlob.Properties;
            if (properties.ContentType != "audio/mpeg")
                return;

            using var stream = await myBlob.OpenReadAsync().ConfigureAwait(false);
            using var mp3 = new Mp3FileReader(stream);
            using var pcm = WaveFormatConversionStream.CreatePcmStream(mp3);

            var wavFile = Path.GetTempFileName();
            try
            {
                WaveFileWriter.CreateWaveFile(wavFile, pcm);


                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                using var audioInput = AudioConfig.FromWavFileInput(wavFile);
                using var recognizer = new SpeechRecognizer(_speechConfig, audioInput);

                var watch = Stopwatch.StartNew();
                var results = new List<Phrase>();

                var complete = new TaskCompletionSource<bool>();
                recognizer.SpeechEndDetected += (o, e) => complete.TrySetResult(true);
                recognizer.Recognized += (o, e) =>
                {
                    var r = e.Result;
                    var offset = TimeSpan.FromTicks(r.OffsetInTicks);

                    log.LogInformation("Found phrase at: {offset} - '{text}'", offset, r.Text);
                    results.Add(new Phrase
                    {
                        offset = offset,
                        duration = r.Duration,
                        text = r.Text,
                    });
                };

                await Task.WhenAll(
                    recognizer.StartContinuousRecognitionAsync(),
                    complete.Task
                ).ConfigureAwait(false);

                await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
            }
            finally { File.Delete(wavFile); }
        }


        private struct Phrase
        {
            public TimeSpan offset { get; set; }
            public TimeSpan duration { get; set; }
            public string text { get; set; }
        }
    }
}
