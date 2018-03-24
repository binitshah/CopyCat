using System;
using System.Collections.Generic;
using Windows.Kinect;
using Microsoft.Kinect.VisualGestureBuilder;

using System.IO;
using UnityEngine;
/// <summary>
/// Gesture Detector class which listens for VisualGestureBuilderFrame events from the service
/// and updates the associated GestureResultView object with the latest results for the 'Seated' gesture
/// </summary>
internal class GestureDetector : IDisposable
{
    /// <summary> Path to the gesture database that was trained with VGB </summary>
    private readonly string gestureSw1 = @"Database\sw1.gbd";
    private readonly string gestureSw2 = @"Database\sw2.gbd";
    //private readonly string gestureBg = @"Database\bg1.gbd";
    private readonly string gestureCt1 = @"Database\ct.gbd";
    private readonly string gestureCt2 = @"Database\ct2.gbd";

    /// <summary> Name of the discrete gesture in the database that we want to track </summary>
    private readonly string sw1Name = "sw1";
    private readonly string sw2Name = "sw2";
    //private readonly string bgName = "bg1";
    private readonly string ct1Name = "ct_Left";
    private readonly string ct2Name = "ct2";

    /// <summary> Gesture frame source which should be tied to a body tracking ID </summary>
    private VisualGestureBuilderFrameSource vgbFrameSource = null;

    /// <summary> Gesture frame reader which will handle gesture events coming from the sensor </summary>
    private VisualGestureBuilderFrameReader vgbFrameReader = null;

    public int frameCount = 0;
    public bool sw1Done = false;
    public bool sw2Done = false;

    public bool ct1Done = false;
    public bool ct2Done = false;
    /// <summary>
    /// Initializes a new instance of the GestureDetector class along with the gesture frame source and reader
    /// </summary>
    /// <param name="kinectSensor">Active sensor to initialize the VisualGestureBuilderFrameSource object with</param>
    /// <param name="gestureResultView">GestureResultView object to store gesture results of a single body to</param>
    public GestureDetector(KinectSensor kinectSensor)
    {
        string prefix = @"C:\Unity Projects\CopyCat 2D Demo\Assets\Scripts\";
        
        //if (gestureResultView == null)
        //{
        //    throw new ArgumentNullException("gestureResultView");
        //}

        //this.GestureResultView = gestureResultView;
        //this.GestureResultView = null;

        // create the vgb source. The associated body tracking ID will be set when a valid body frame arrives from the sensor.
        this.vgbFrameSource = VisualGestureBuilderFrameSource.Create(kinectSensor, 0);
        this.vgbFrameSource.TrackingIdLost += this.Source_TrackingIdLost;

        // open the reader for the vgb frames
        this.vgbFrameReader = this.vgbFrameSource.OpenReader();
        if (this.vgbFrameReader != null)
        {
            this.vgbFrameReader.IsPaused = true;
            this.vgbFrameReader.FrameArrived += this.Reader_GestureFrameArrived;
        }

        // load the 'Seated' gesture from the gesture database

        using (VisualGestureBuilderDatabase database = VisualGestureBuilderDatabase.Create(prefix + this.gestureSw1))
        {
            // we could load all available gestures in the database with a call to vgbFrameSource.AddGestures(database.AvailableGestures), 
            // but for this program, we only want to track one discrete gesture from the database, so we'll load it by name
            foreach (Gesture gesture in database.AvailableGestures)
            {
                if (gesture.Name.Equals(this.sw1Name))
                {
                    this.vgbFrameSource.AddGesture(gesture);
                }
            }
        }
        using (VisualGestureBuilderDatabase database = VisualGestureBuilderDatabase.Create(prefix + this.gestureSw2))
        {
            // we could load all available gestures in the database with a call to vgbFrameSource.AddGestures(database.AvailableGestures), 
            // but for this program, we only want to track one discrete gesture from the database, so we'll load it by name
            foreach (Gesture gesture in database.AvailableGestures)
            {
                if (gesture.Name.Equals(this.sw2Name))
                {
                    this.vgbFrameSource.AddGesture(gesture);
                }
            }
        }
        using (VisualGestureBuilderDatabase database = VisualGestureBuilderDatabase.Create(prefix + this.gestureCt1))
        {
            // we could load all available gestures in the database with a call to vgbFrameSource.AddGestures(database.AvailableGestures), 
            // but for this program, we only want to track one discrete gesture from the database, so we'll load it by name
            foreach (Gesture gesture in database.AvailableGestures)
            {
                if (gesture.Name.Equals(this.ct1Name))
                {
                    this.vgbFrameSource.AddGesture(gesture);
                }
            }
        }
        using (VisualGestureBuilderDatabase database = VisualGestureBuilderDatabase.Create(prefix + this.gestureCt2))
        {
            // we could load all available gestures in the database with a call to vgbFrameSource.AddGestures(database.AvailableGestures), 
            // but for this program, we only want to track one discrete gesture from the database, so we'll load it by name
            foreach (Gesture gesture in database.AvailableGestures)
            {
                if (gesture.Name.Equals(this.ct2Name))
                {
                    this.vgbFrameSource.AddGesture(gesture);
                }
            }
        }


    }

    /// <summary> Gets the GestureResultView object which stores the detector results for display in the UI </summary>
    //public GestureResultView GestureResultView { get; private set; }

    /// <summary>
    /// Gets or sets the body tracking ID associated with the current detector
    /// The tracking ID can change whenever a body comes in/out of scope
    /// </summary>
    public ulong TrackingId
    {
        get
        {
            return this.vgbFrameSource.TrackingId;
        }

        set
        {
            if (this.vgbFrameSource.TrackingId != value)
            {
                this.vgbFrameSource.TrackingId = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether or not the detector is currently paused
    /// If the body tracking ID associated with the detector is not valid, then the detector should be paused
    /// </summary>
    public bool IsPaused
    {
        get
        {
            return this.vgbFrameReader.IsPaused;
        }

        set
        {
            if (this.vgbFrameReader.IsPaused != value)
            {
                this.vgbFrameReader.IsPaused = value;
            }
        }
    }

    /// <summary>
    /// Disposes all unmanaged resources for the class
    /// </summary>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the VisualGestureBuilderFrameSource and VisualGestureBuilderFrameReader objects
    /// </summary>
    /// <param name="disposing">True if Dispose was called directly, false if the GC handles the disposing</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (this.vgbFrameReader != null)
            {
                this.vgbFrameReader.FrameArrived -= this.Reader_GestureFrameArrived;
                this.vgbFrameReader.Dispose();
                this.vgbFrameReader = null;
            }

            if (this.vgbFrameSource != null)
            {
                this.vgbFrameSource.TrackingIdLost -= this.Source_TrackingIdLost;
                this.vgbFrameSource.Dispose();
                this.vgbFrameSource = null;
            }
        }
    }

    /// <summary>
    /// Handles gesture detection results arriving from the sensor for the associated body tracking Id
    /// </summary>
    /// <param name="sender">object sending the event</param>
    /// <param name="e">event arguments</param>
    private void Reader_GestureFrameArrived(object sender, VisualGestureBuilderFrameArrivedEventArgs e)
    {
        VisualGestureBuilderFrameReference frameReference = e.FrameReference;
        using (VisualGestureBuilderFrame frame = frameReference.AcquireFrame())
        {
            if (frame != null)
            {
                // get the discrete gesture results which arrived with the latest frame
                Dictionary<Gesture, DiscreteGestureResult> discreteResults = frame.DiscreteGestureResults;
                Dictionary<Gesture, ContinuousGestureResult> contResults = frame.ContinuousGestureResults;

                if (discreteResults != null)
                {
                    // we only have one gesture in this source object, but you can get multiple gestures
                    foreach (Gesture gesture in this.vgbFrameSource.Gestures)
                    {

                        if (gesture.Name.Equals(this.sw1Name) && gesture.GestureType == GestureType.Discrete)
                        {
                            DiscreteGestureResult result = null;
                            discreteResults.TryGetValue(gesture, out result);

                            if (result != null)
                            {
                                // update the GestureResultView object with new gesture result values
                                if (result.Confidence > 0.25)
                                {
                                    sw1Done = true;
                                    //Console.Write("\nsw1Name RESULT: " + result.Detected + "|" + result.Confidence);
                                }
                                //this.GestureResultView.UpdateGestureResult(true, result.Detected, result.Confidence);
                            }
                        }
                        if (gesture.Name.Equals(this.sw2Name) && gesture.GestureType == GestureType.Discrete)
                        {
                            DiscreteGestureResult result = null;
                            discreteResults.TryGetValue(gesture, out result);

                            if (result != null)
                            {
                                // update the GestureResultView object with new gesture result values
                                if (result.Confidence > 0.23 & sw1Done)
                                {
                                    sw2Done = true;
                                    //Console.Write("\nsw2Name RESULT: " + result.Detected + "|" + result.Confidence);                                    
                                }
                                //this.GestureResultView.UpdateGestureResult(true, result.Detected, result.Confidence);
                            }
                        }
                        //-----------------------------------------SW1 & SW2 ------------------------------------------------------------------------
                        //---------------------------------------------------------------------------------------------------------------------------
                        if (gesture.Name.Equals(this.ct1Name) && gesture.GestureType == GestureType.Discrete)
                        {
                            DiscreteGestureResult result = null;
                            discreteResults.TryGetValue(gesture, out result);

                            if (result != null)
                            {
                                // update the GestureResultView object with new gesture result values
                                if (result.Confidence >= 0.95)
                                {
                                    ct1Done = true;
                                    //Console.Write("\nct1Name RESULT: " + result.Detected + " | " + result.Confidence + " | " + frameCount);
                                }

                                //this.GestureResultView.UpdateGestureResult(true, result.Detected, result.Confidence);
                            }
                        }
                        if (gesture.Name.Equals(this.ct2Name) && gesture.GestureType == GestureType.Discrete)
                        {
                            DiscreteGestureResult result = null;
                            discreteResults.TryGetValue(gesture, out result);

                            if (result != null)
                            {
                                // update the GestureResultView object with new gesture result values
                                if (result.Confidence >= 0.95 && ct1Done)
                                {
                                    ct2Done = true;
                                    //Console.Write("\nct2Name RESULT: " + result.Detected + "|" + result.Confidence + " | " + frameCount);
                                }

                                //this.GestureResultView.UpdateGestureResult(true, result.Detected, result.Confidence);
                            }
                        }
                        //                            
                    }
                }
                frameCount++;
            }
        }
    }

    /// <summary>
    /// Handles the TrackingIdLost event for the VisualGestureBuilderSource object
    /// </summary>
    /// <param name="sender">object sending the event</param>
    /// <param name="e">event arguments</param>
    private void Source_TrackingIdLost(object sender, TrackingIdLostEventArgs e)
    {
        // update the GestureResultView object to show the 'Not Tracked' image in the UI
        //this.GestureResultView.UpdateGestureResult(false, false, 0.0f);
    }
}