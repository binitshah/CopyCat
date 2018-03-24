using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuHandler : MonoBehaviour {


	// Button Handlers
	public void PlayButtonPressed() {
		SceneManager.LoadScene("Main");
	}

	void Start () {
		
	}

	void Update () {
		
	}
}
