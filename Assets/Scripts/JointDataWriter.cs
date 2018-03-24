using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using System.IO;

public class JointDataWriter {

    private int phrase_file_count;
    private string current_phrase;
    private bool shouldStop = false;
    private FileStream sourceStream;
    private string filePath;
    private bool paused;
    private bool writeOn;

    public JointDataWriter()
    {
        this.sourceStream = null;
    }

    public void pause() { this.paused = true; }
    public void unpause() { this.paused = false; }

    public void setCurrentPhrase(string p)
    {
        //if (!this.paused){
        this.current_phrase = p;
        this.phrase_file_count = 1;
        //}
    }

    public void endPhrase()
    {
        //if (!this.paused){
        if (this.sourceStream != null)
        {
            this.sourceStream.Close();
            this.phrase_file_count++;
        }
        this.writeOn = false;
        //}
    }

    public void deleteLastSample(int session_number, string dataWritePath)
    {
        //if (!this.paused){
        if (this.sourceStream != null)
        {
            this.sourceStream.Close();
        }
        this.phrase_file_count--;
        File.Delete(dataWritePath + this.current_phrase + "\\" + session_number + "\\" + this.current_phrase + "_" + session_number + ".txt");
        this.writeOn = false;
        //}
    }

    public string startNewPhrase(int session_number, string dataWritePath)
    {
        this.writeOn = true;
        this.filePath = dataWritePath + this.current_phrase + "\\" + session_number + "\\" + this.current_phrase + "_" + session_number + ".txt";
        Debug.Log(this.filePath);
        this.sourceStream = new FileStream(this.filePath, FileMode.Append, FileAccess.Write, FileShare.None, bufferSize: 25000, useAsync: true);
        return this.filePath;
    }

    public void writeData(string s)
    {
        //await WriteTextAsync(this.filePath, b);
        //if (!this.paused){
        if (this.writeOn)
        {
            byte[] b = Encoding.Unicode.GetBytes(s);
            sourceStream.Write(b, 0, b.Length);
        }
        //}
    }

    public void markAsBadSample(int session_number, string dataWritePath)
    {

        String bad_sample_file = this.filePath = dataWritePath + this.current_phrase + "\\" + session_number + "\\" + "bad_sample.txt";

        FileStream f = System.IO.File.Create(bad_sample_file);
        f.Close();
    }
}
