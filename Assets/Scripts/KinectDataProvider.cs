using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Windows.Kinect;

public class KinectDataProvider : MonoBehaviour {

	public bool recording = false;
	public GameObject levelLoaderObject;

	private enum RunState {
		Writing,
		NotRunning,
		Recording
	}
	private RunState runState = RunState.NotRunning;
	private bool raisedLeftHand = false;
	private int sessionNum;
	private string mainDir;
	private bool startMode = false;
	private Queue<byte[]> colorQueue;
	private Queue<ushort[]> depthQueue;
	private int framesCapturedInPhrase = 0;
	private KinectSensor kinectSensor;

	// Multi
	private MultiSourceFrameReader multiFrameReader;
	private JointDataWriter jointDataWriter;

	// Color
	private ColorFrameReader colorFrameReader;
	private Texture2D colorFrameTexture;
	private byte[] colorData;

	// Body
	private BodyFrameReader bodyFrameReader;
	private Body[] bodies;
	private List<GestureDetector> gestureDetectorList;


	// Public getters and events
	public int ColorWidth { get; private set; }
	public int ColorHeight { get; private set; }

	public Texture2D GetColorFrame() {
		return colorFrameTexture;
	}

	public void recordButtonPressed() {
		recording = !recording;
	}

	void Start () {
		if (levelLoaderObject == null) {
			throw new MissingReferenceException("Missing reference to Level Loader GameObject.");
		}
		mainDir = @"C:\PhraseData\" + levelLoaderObject.GetComponent<LevelLoader>().currPhrase + @"\";
		colorQueue = new Queue<byte[]>();
		depthQueue = new Queue<ushort[]>();
		kinectSensor = KinectSensor.GetDefault();

		if (kinectSensor != null) {
			if (!kinectSensor.IsOpen) {
				kinectSensor.Open();
			}

			// Initialize readers
			colorFrameReader = kinectSensor.ColorFrameSource.OpenReader();
			multiFrameReader = kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Depth);
			bodyFrameReader = kinectSensor.BodyFrameSource.OpenReader();
			bodyFrameReader.FrameArrived += BodyFrameArrived;

			// Setup multi vars
			jointDataWriter = new JointDataWriter();
			this.sessionNum = 1;

			// Setup color vars
			var frameMetadata = kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Rgba);
			ColorWidth = frameMetadata.Width;
			ColorHeight = frameMetadata.Height;
			colorFrameTexture = new Texture2D(ColorWidth, ColorHeight, TextureFormat.RGBA32, false);
			colorData = new byte[frameMetadata.BytesPerPixel * frameMetadata.LengthInPixels];

			// Setup body vars
			gestureDetectorList = new List<GestureDetector>();
			for (int i = 0; i < kinectSensor.BodyFrameSource.BodyCount; ++i) {
				GestureDetector detector = new GestureDetector(this.kinectSensor);
				this.gestureDetectorList.Add(detector);
			}
		}
	}

	void Update () {
		if (colorFrameReader != null) {
			var frame = colorFrameReader.AcquireLatestFrame();

			if (frame != null) {
				frame.CopyConvertedFrameDataToArray(colorData, ColorImageFormat.Rgba);
				colorFrameTexture.LoadRawTextureData(colorData);
				colorFrameTexture.Apply();

				frame.Dispose();
				frame = null;
			}
		}
	}

	private void BodyFrameArrived(object sender, BodyFrameArrivedEventArgs e) {
		bool dataReceived = false;
		using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame()) {
			if (bodyFrame != null) {
				if (bodies == null) {
					bodies = new Body[bodyFrame.BodyCount];
				}

				bodyFrame.GetAndRefreshBodyData(bodies);
				dataReceived = true;
			}
		}

		if (this.bodies != null) {
			for (int i = 0; i < kinectSensor.BodyFrameSource.BodyCount; ++i) {
				Body body = bodies[i];

				Windows.Kinect.Joint handr = body.Joints[JointType.HandRight];         //11
				Windows.Kinect.Joint handl = body.Joints[JointType.HandLeft];          //7
				Windows.Kinect.Joint thumbr = body.Joints[JointType.ThumbRight];       //24
				Windows.Kinect.Joint thumbl = body.Joints[JointType.ThumbLeft];        //22
				Windows.Kinect.Joint tipr = body.Joints[JointType.HandTipRight];       //23
				Windows.Kinect.Joint tipl = body.Joints[JointType.HandTipLeft];        //21

				Windows.Kinect.Joint hipr = body.Joints[JointType.HipRight];           //16
				Windows.Kinect.Joint hipl = body.Joints[JointType.HipLeft];            //12
				Windows.Kinect.Joint spinebase = body.Joints[JointType.SpineBase];     //0
				Windows.Kinect.Joint spinemid = body.Joints[JointType.SpineMid];

				double spineDifferenceY = Math.Abs(spinebase.Position.Y - spinemid.Position.Y);
				double distFromBase = (spineDifferenceY * 2.0) / 3.0; //Take 2/3rds the distance from the spine base.
//				double threshold = spinebase.Position.Y + distFromBase;

				double handlY = handl.Position.Y;
				double handrY = handr.Position.Y;

				if (!recording) {
					if (runState == RunState.Recording) {
						if (!raisedLeftHand) {
							// erase session data
							startMode = false;
							jointDataWriter.deleteLastSample(sessionNum, mainDir);
							colorQueue.Clear();
							depthQueue.Clear();
							runState = RunState.NotRunning;
							raisedLeftHand = false;
						} else {
							// save session data
							startMode = false;
							jointDataWriter.endPhrase();
							runState = RunState.NotRunning;
							raisedLeftHand = false;
						}
					}
				} else {
					if (!startMode) {
						startMode = true;
						sessionNum++;
						string filePath = mainDir + "\\" + sessionNum;
						System.IO.Directory.CreateDirectory(filePath);
						framesCapturedInPhrase = 0;
					}
					runState = RunState.Recording;
					if (recording) {
						raisedLeftHand = true;
					}
				}
			}
		}

		if (dataReceived) {
			if (bodies != null) {
				for (int i = 0; i < kinectSensor.BodyFrameSource.BodyCount; ++i) {
					Body body = bodies[i];
					ulong trackingId = body.TrackingId;

					if (trackingId != 0) {
						string msg = prepareTCPMessage(body);
						if (startMode) {
							framesCapturedInPhrase++;
						}
						jointDataWriter.writeData(msg + "\n");
					}

					if (trackingId != gestureDetectorList[i].TrackingId) {
						gestureDetectorList[i].TrackingId = trackingId;
						gestureDetectorList[i].IsPaused = trackingId == 0;
					}
				}
			}
		}
	}

	private string prepareTCPMessage(Body body)
	{
		string msg = "";

		Windows.Kinect.Joint head = body.Joints[JointType.Head];               //3
		Windows.Kinect.Joint neck = body.Joints[JointType.Neck];               //2
		Windows.Kinect.Joint shoulderr = body.Joints[JointType.ShoulderRight]; //8
		Windows.Kinect.Joint shoulderl = body.Joints[JointType.ShoulderLeft];  //4
		Windows.Kinect.Joint spinesh = body.Joints[JointType.SpineShoulder];   //20

		Windows.Kinect.Joint elbowr = body.Joints[JointType.ElbowRight];       //9
		Windows.Kinect.Joint elbowl = body.Joints[JointType.ElbowLeft];        //5
		Windows.Kinect.Joint wristr = body.Joints[JointType.WristRight];       //10
		Windows.Kinect.Joint wristl = body.Joints[JointType.WristLeft];        //6
		Windows.Kinect.Joint handr = body.Joints[JointType.HandRight];         //11
		Windows.Kinect.Joint handl = body.Joints[JointType.HandLeft];          //7
		Windows.Kinect.Joint thumbr = body.Joints[JointType.ThumbRight];       //24
		Windows.Kinect.Joint thumbl = body.Joints[JointType.ThumbLeft];        //22
		Windows.Kinect.Joint tipr = body.Joints[JointType.HandTipRight];       //23
		Windows.Kinect.Joint tipl = body.Joints[JointType.HandTipLeft];        //21

		Windows.Kinect.Joint hipr = body.Joints[JointType.HipRight];           //16
		Windows.Kinect.Joint hipl = body.Joints[JointType.HipLeft];            //12
		Windows.Kinect.Joint spinebase = body.Joints[JointType.SpineBase];     //0
		Windows.Kinect.Joint kneer = body.Joints[JointType.KneeRight];         //17
		Windows.Kinect.Joint kneel = body.Joints[JointType.KneeLeft];          //13

		double l0 = Math.Round(Math.Sqrt(Math.Pow((neck.Position.X - shoulderl.Position.X), 2) + Math.Pow((neck.Position.Y - shoulderl.Position.Y), 2) + Math.Pow((neck.Position.Z - shoulderl.Position.Z), 2)), 5);
		double r0 = Math.Round(Math.Sqrt(Math.Pow((neck.Position.X - shoulderr.Position.X), 2) + Math.Pow((neck.Position.Y - shoulderr.Position.Y), 2) + Math.Pow((neck.Position.Z - shoulderr.Position.Z), 2)), 5);
		double l1 = Math.Round(Math.Sqrt(Math.Pow((shoulderl.Position.X - elbowl.Position.X), 2) + Math.Pow((shoulderl.Position.Y - elbowl.Position.Y), 2) + Math.Pow((shoulderl.Position.Z - elbowl.Position.Z), 2)), 5);
		double r1 = Math.Round(Math.Sqrt(Math.Pow((shoulderr.Position.X - elbowr.Position.X), 2) + Math.Pow((shoulderr.Position.Y - elbowr.Position.Y), 2) + Math.Pow((shoulderr.Position.Z - elbowr.Position.Z), 2)), 5);
		double l2 = Math.Round(Math.Sqrt(Math.Pow((elbowl.Position.X - wristl.Position.X), 2) + Math.Pow((elbowl.Position.Y - wristl.Position.Y), 2) + Math.Pow((elbowl.Position.Z - wristl.Position.Z), 2)), 4);
		double r2 = Math.Round(Math.Sqrt(Math.Pow((elbowr.Position.X - wristr.Position.X), 2) + Math.Pow((elbowr.Position.Y - wristr.Position.Y), 2) + Math.Pow((elbowr.Position.Z - wristr.Position.Z), 2)), 4);

		double norm = (l0 + l1 + l2 + r0 + r1 + r2) / 2.0;

		Windows.Kinect.Joint[] joints = { head, neck, shoulderr, shoulderl, spinesh, elbowr, elbowl, wristr, wristl, handr, handl, thumbr, thumbl, tipr, tipl, hipr, hipl, spinebase, kneer, kneel };
		string msg_points = "";
		foreach (Windows.Kinect.Joint j in joints)
		{
			msg_points += "" + Math.Round(j.Position.X / norm, 5) + " " + Math.Round(j.Position.Y / norm, 5) + " " + Math.Round(j.Position.Z / norm, 5) + " ";
		}

		//------------------------------------------------------------------------------------------------------------------------------------
		//------------------------------------------------------------------------------------------------------------------------------------
		JointType[] joint_types = {
			JointType.Head,
			JointType.Neck,
			JointType.ShoulderRight,
			JointType.ShoulderLeft,
			JointType.SpineShoulder,
			JointType.ElbowRight,
			JointType.ElbowLeft,
			JointType.WristRight,
			JointType.WristLeft,
			JointType.HandRight,
			JointType.HandLeft,
			JointType.ThumbRight,
			JointType.ThumbLeft,
			JointType.HandTipRight,
			JointType.HandTipLeft,
			JointType.HipRight,
			JointType.HipLeft,
			JointType.SpineBase
		};//, JointType.KneeRight, JointType.KneeLeft };

		int joint_count = 0;
		foreach (JointType j in joint_types)
		{
			Windows.Kinect.Vector4 quat = body.JointOrientations[j].Orientation;
			double msg_w = Math.Round(quat.W, 7);
			double msg_x = Math.Round(quat.X, 7);
			double msg_y = Math.Round(quat.Y, 7);
			double msg_z = Math.Round(quat.Z, 7);
			//double msg_x = Math.Round((j.Position.X - neck.Position.X) / norm, 5);double msg_y = Math.Round((j.Position.Y - neck.Position.Y) / norm, 5);double msg_z = Math.Round((j.Position.Z - neck.Position.Z) / norm, 5);
			msg += "" + msg_w + " " + msg_x + " " + msg_y + " " + msg_z + " ";
			joint_count++;
		}
		//Console.WriteLine(msgCount++ +" | " + msg.Length + " | " + joint_count);

		msg = msg + " ||| " + msg_points;
		return msg;
	}

	void OnApplicationQuit() {
		if (colorFrameReader != null) {
			colorFrameReader.Dispose();
			colorFrameReader = null;
		}
		if (kinectSensor != null) {
			if (kinectSensor.IsOpen) {
				kinectSensor.Close();
			}
			kinectSensor = null;
		}
	}
}
