using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using TMPro;

public class MainManager : MonoBehaviour
{
    private string _apiKey = ""; // OpenAI API Keyを入力
    private string _apiUrl = "https://api.openai.com/v1/audio/transcriptions";

    private AudioClip _audioClip;
    private bool _isRecording = false;
    private byte[] _wavData;

    public GameObject startRecordingButton;
    public GameObject stopRecordingButton;
    public GameObject transcribeButton;

    public TextMeshProUGUI transcriptionText;

    private void Awake()
    {
        startRecordingButton.SetActive(true);
        stopRecordingButton.SetActive(false);
        transcribeButton.SetActive(false);
    }

    public void OnStartRecordingButtonClicked()
    {
        startRecordingButton.SetActive(false);
        stopRecordingButton.SetActive(true);
        transcribeButton.SetActive(false);
        
        StartRecording();
    }

    public void OnStopRecordingButtonClicked()
    {
        startRecordingButton.SetActive(true);
        stopRecordingButton.SetActive(false);
        transcribeButton.SetActive(true);
        
        StopRecording();
    }

    public void OnTranscribeButtonClicked()
    {
        StartTranscription();
    }

    private void StartRecording()
    {
        if (_isRecording) return;

        // マイクから音声を録音（サンプリングレート16kHz、モノラル）
        _audioClip = Microphone.Start(null, false, 15, 16000);
        _isRecording = true;
        Debug.Log("Recording started...");
    }

    private void StopRecording()
    {
        if (!_isRecording) return;
        
        Microphone.End(null);
        _isRecording = false;
        Debug.Log("Recording stopped.");
        _wavData = ConvertAudioClipToWav(_audioClip);
        Debug.Log("Data size: " + _wavData.Length);
    }

    private byte[] ConvertAudioClipToWav(AudioClip audioClip)
    {
        var samples = new float[audioClip.samples * audioClip.channels];
        audioClip.GetData(samples, 0);

        MemoryStream stream = new MemoryStream();
        int sampleRate = audioClip.frequency;
        int channels = audioClip.channels;
        int samplesCount = audioClip.samples;
        
        // WAVヘッダーを追加
        stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"), 0, 4);
        stream.Write(BitConverter.GetBytes(36 + samplesCount * channels * 2), 0, 4);
        stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"), 0, 4);
        stream.Write(System.Text.Encoding.ASCII.GetBytes("fmt "), 0, 4);
        stream.Write(BitConverter.GetBytes(16), 0, 4);
        stream.Write(BitConverter.GetBytes((short)1), 0, 2);
        stream.Write(BitConverter.GetBytes((short)channels), 0, 2);
        stream.Write(BitConverter.GetBytes(sampleRate), 0, 4);
        stream.Write(BitConverter.GetBytes(sampleRate * channels * 2), 0, 4);
        stream.Write(BitConverter.GetBytes((short)(channels * 2)), 0, 2);
        stream.Write(BitConverter.GetBytes((short)16), 0, 2);
        
        // "data"チャンクを追加
        stream.Write(System.Text.Encoding.ASCII.GetBytes("data"), 0, 4);
        stream.Write(BitConverter.GetBytes(samplesCount * channels * 2), 0, 4);
        
        // PCMデータを追加
        foreach (var sample in samples)
        {
            short intSample = (short)(sample * short.MaxValue);
            stream.Write(BitConverter.GetBytes(intSample), 0, 2);
        }

        return stream.ToArray();
    }

    private void StartTranscription()
    {
        if (_wavData != null)
        {
            StartCoroutine(UploadAudio());
            transcriptionText.text = "...";
            Debug.Log("Transcription started.");
        }
        else
        {
            Debug.LogError("No audio data available for transcription.");
        }
    }

    private IEnumerator UploadAudio()
    {
        // ファイルパスを設定して保存する
        string filePath = Path.Combine(Application.persistentDataPath, "recorded_audio.wav");
        File.WriteAllBytes(filePath, _wavData);
        
        // APIへのリクエストを準備
        var form = new WWWForm();
        form.AddField("model", "whisper-1");
        form.AddBinaryData("file", File.ReadAllBytes(filePath), "recorded_audio.wav", "audio/wav");

        using (UnityWebRequest request = UnityWebRequest.Post(_apiUrl, form))
        {
            request.SetRequestHeader("Authorization", "Bearer " + _apiKey);
            
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                TranscriptionResponse response = JsonUtility.FromJson<TranscriptionResponse>(request.downloadHandler.text);
                Debug.Log("Transcription: " + response.text);
                transcriptionText.text = response.text;
            }
            else
            {
                Debug.LogError("Error: " + request.error);
            }
        }
    }

    [Serializable]
    public class TranscriptionResponse
    {
        public string text;
    }
}
