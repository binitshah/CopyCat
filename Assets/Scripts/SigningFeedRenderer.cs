using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SigningFeedRenderer : MonoBehaviour {

	public GameObject kinectObject;

	private Renderer kinectColorRenderer;
	private KinectDataProvider kinectDataProvider;

	void Start () {
		// Set initial texture scale of the kinect renderer
		this.kinectColorRenderer = gameObject.GetComponent<Renderer>();
		this.kinectColorRenderer.material.SetTextureScale("_MainTex", new Vector2(-1, 1));
	}

	void Update () {
		// Check to ensure we have the object that runs the kinect data provider
		if (kinectObject == null) {
			return;
		}

		// Get the kinect data provider
		kinectDataProvider = kinectObject.GetComponent<KinectDataProvider>();
		if (kinectDataProvider == null) {
			return;
		}

		// Display kinect data
		this.kinectColorRenderer.material.mainTexture = kinectDataProvider.GetColorFrame();
	}
}
