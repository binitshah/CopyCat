using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using UnityEditor;
using SimpleJSON;

public class LevelLoader : MonoBehaviour {

	public bool debugMode = false;
	static public int currLevel = 1;
	public string currPhrase;
	public TextAsset phrasesJSONFile;
	public GameObject hintVideoRenderer;
	public GameObject signingVideoRenderer;
	public GameObject irisRenderer;
	public GameObject magicsRenderer;
	public GameObject enemyAndItemRenderer;
	public GameObject currBackgroundRenderer;
	public GameObject nextBackgroundRenderer;

	private List<string> usedPhrases;
	private List<string> unusedPhrases;
	private Dictionary<string, string> phraseMetaData;
	private enum LevelState {
		Loading,
		WaitingForAction,
		Listening,
		Evaluating,
		Confused,
		ConfusedInterim,
		SendingMagic,
		SendingMagicInterim,
		Transitioning,
		Exiting
	};
	private LevelState currLevelState;
	private string currPhraseFormat;
	private float countdown;
	private Vector3 currBackgroundInitPosition;
	private Vector3 nextBackgroundInitPosition;
	private Vector3 irisInitPosition;
	private Vector3 enemyAndItemInitPosition;

	// public event functions
	public void PlayHint() {
		VideoPlayer hintVideoPlayer = hintVideoRenderer.GetComponent<VideoPlayer>();
		if (hintVideoPlayer.isPlaying) {
			hintVideoPlayer.Pause();
			hintVideoPlayer.frame = 1;
			hintVideoPlayer.Play();
		} else {
			hintVideoRenderer.SetActive(true);
			hintVideoPlayer.Play();
			hintVideoPlayer.loopPointReached += HintVideoEndReached;
		}
	}

	public void ShowSigningFeed() {
		VideoPlayer hintVideoPlayer = hintVideoRenderer.GetComponent<VideoPlayer>();
		if (hintVideoPlayer.isPlaying) {
			hintVideoPlayer.Stop();
		}
		hintVideoRenderer.SetActive(false);
		if (signingVideoRenderer.activeInHierarchy) {
			signingVideoRenderer.SetActive(false);
			currLevelState = LevelState.Evaluating;
		} else {
			signingVideoRenderer.SetActive(true);
			currLevelState = LevelState.Listening;
		}
	}

	void Start () {
		if (this.phrasesJSONFile == null) {
			throw new MissingReferenceException("Missing reference to phrases.json.");
		}

		// initialize vars and load phrase data
		var json = JSON.Parse(this.phrasesJSONFile.text);

		this.phraseMetaData = new Dictionary<string, string>();
		this.phraseMetaData.Add("version", json["phrase_metadata"]["version"]);
		this.phraseMetaData.Add("minPhraseLength", json["phrase_metadata"]["min_phrase_length"]);
		this.phraseMetaData.Add("maxPhraseLength", json["phrase_metadata"]["max_phrase_length"]);
		this.phraseMetaData.Add("totalNumPhrases", json["phrase_metadata"]["total_num_phrases"]);

		int phraseLength = currLevel + 2; // level (1, 2, 3) -> (3, 4, 5) word phrases
		this.unusedPhrases = new List<string>();
		for (int i = 0; i < json["data"][phraseLength + "-word"]["phrases"].Count; i++) {
			this.unusedPhrases.Add(json["data"][phraseLength + "-word"]["phrases"][i]);
		}
		this.currPhraseFormat = json["data"][phraseLength + "-word"]["format"];
		this.usedPhrases = new List<string>();

		// load current phrase
		this.currPhrase = PhraseChooser();
		if (currPhrase == null) {
			Debug.Log("No Phrases available. Game quit.");
			Application.Quit();
		}

		// Load enemy and item now so the transitions do not jump later
		foreach (Transform child in this.enemyAndItemRenderer.transform) {
			GameObject.Destroy(child.gameObject);
		}
		UnityEngine.Object enemyAndItemPrefab = AssetDatabase.LoadAssetAtPath("Assets/Prefabs/" + this.currPhrase + ".prefab", typeof(GameObject));
//		UnityEngine.Object enemyAndItemPrefab = Resources.Load("Prefabs/" + this.currPhrase + ".prefab", typeof(GameObject));
//		if (enemyAndItemPrefab == null) {
//			throw new NullReferenceException("Could not find enemyAndItem prefab.");
//		}
		GameObject enemyAndItem = Instantiate(enemyAndItemPrefab, Vector3.zero, Quaternion.identity) as GameObject;
		enemyAndItem.transform.parent = this.enemyAndItemRenderer.transform;
		enemyAndItem.transform.position = this.enemyAndItemRenderer.transform.position;

		this.currBackgroundInitPosition = this.currBackgroundRenderer.transform.position;
		this.nextBackgroundInitPosition = this.nextBackgroundRenderer.transform.position;
		this.enemyAndItemInitPosition = this.enemyAndItemRenderer.transform.position;
		this.irisInitPosition = this.irisRenderer.transform.position;

		// start the game off by loading the right assets
		this.currLevelState = LevelState.Loading;
	}
	
	void Update () {
		if (this.currLevelState == LevelState.Loading) {
			// Load current phrase's hint video
			VideoPlayer hintVideoPlayer = this.hintVideoRenderer.GetComponent<VideoPlayer>();
			hintVideoPlayer.source = VideoSource.VideoClip;
			hintVideoPlayer.isLooping = false;
			hintVideoPlayer.audioOutputMode = VideoAudioOutputMode.None;
			VideoClip hintVideo = (VideoClip) AssetDatabase.LoadAssetAtPath("Assets/Videos/Hints/" + currPhrase + ".mp4", typeof(VideoClip));
			hintVideoPlayer.clip = hintVideo;

			// Load iris
			foreach (Transform child in this.irisRenderer.transform) {
				GameObject.Destroy(child.gameObject);
			}
			UnityEngine.Object irisMiaoPrefab = AssetDatabase.LoadAssetAtPath("Assets/Prefabs/cat_miao.prefab", typeof(GameObject));
//			UnityEngine.Object irisMiaoPrefab = Resources.Load("Prefabs/cat_miao.prefab", typeof(GameObject));
//			if (irisMiaoPrefab == null) {
//				throw new NullReferenceException("Could not find irisMiaos prefab.");
//			}
			GameObject irisMiao = Instantiate(irisMiaoPrefab, Vector3.zero, Quaternion.identity) as GameObject;
			irisMiao.transform.parent = this.irisRenderer.transform;
			irisMiao.transform.position = this.irisRenderer.transform.position;

			// Set Magics off
			this.magicsRenderer.SetActive(false);

			// Load current and next background
			this.currBackgroundRenderer.transform.position = currBackgroundInitPosition;
			this.nextBackgroundRenderer.transform.position = nextBackgroundInitPosition;
			int maxLevel = Int32.Parse(this.phraseMetaData["maxPhraseLength"]) - 2;
			string currBackgroundRoom = "room_";
			string nextBackgroundRoom = "room_";
			if (currLevel < maxLevel) {
				currBackgroundRoom += "light_";
				nextBackgroundRoom += "light_";
			} else {
				currBackgroundRoom += "dark_";
				nextBackgroundRoom += "dark_";
			}
			if (this.usedPhrases.Count == 1) {
				currBackgroundRoom += "start";
				nextBackgroundRoom += "middle";
			} else {
				currBackgroundRoom += "middle";
				if (this.unusedPhrases.Count == 0) {
					nextBackgroundRoom += "end";
				} else {
					nextBackgroundRoom += "middle";
				}
			}
			Sprite currBackgroundRoomSprite = (Sprite) AssetDatabase.LoadAssetAtPath("Assets/Graphics/Rooms/" + currBackgroundRoom + ".png", typeof(Sprite));
			Sprite nextBackgroundRoomSprite = (Sprite) AssetDatabase.LoadAssetAtPath("Assets/Graphics/Rooms/" + nextBackgroundRoom + ".png", typeof(Sprite));
			SpriteRenderer currBackgroundSpriteRenderer = this.currBackgroundRenderer.GetComponent<SpriteRenderer>();
			SpriteRenderer nextBackgroundSpriteRenderer = this.nextBackgroundRenderer.GetComponent<SpriteRenderer>();
			currBackgroundSpriteRenderer.sprite = currBackgroundRoomSprite;
			nextBackgroundSpriteRenderer.sprite = nextBackgroundRoomSprite;

			// Set enemy and item position since it's already been loaded
			this.enemyAndItemRenderer.transform.position = enemyAndItemInitPosition;

			this.currLevelState = LevelState.WaitingForAction;
		}

		if (this.currLevelState == LevelState.Evaluating) {
			if (debugMode) {
				this.currLevelState = LevelState.SendingMagic;
			}
		}

		if (this.currLevelState == LevelState.Confused) {
			foreach (Transform child in this.irisRenderer.transform) {
				GameObject.Destroy(child.gameObject);
			}
			UnityEngine.Object irisOneHandPrefab = AssetDatabase.LoadAssetAtPath("Assets/Prefabs/scratch_onehand.prefab", typeof(GameObject));
			GameObject irisOneHand = Instantiate(irisOneHandPrefab, Vector3.zero, Quaternion.identity) as GameObject;
			irisOneHand.transform.parent = this.irisRenderer.transform;
			irisOneHand.transform.position = this.irisRenderer.transform.position;

			this.countdown = 2.0f;
			this.currLevelState = LevelState.ConfusedInterim;
		}

		if (this.currLevelState == LevelState.ConfusedInterim) {
			if (this.countdown > 0.0f) {
				this.countdown -= Time.deltaTime;

				if (this.countdown <= 1.0f) {
					// Show magics
					if (!this.magicsRenderer.activeInHierarchy) {
						foreach (Transform child in this.magicsRenderer.transform) {
							GameObject.Destroy(child.gameObject);
						}
						UnityEngine.Object magicsFailedPrefab = AssetDatabase.LoadAssetAtPath("Assets/Prefabs/magics_failed.prefab", typeof(GameObject));
						GameObject magicsFailed = Instantiate(magicsFailedPrefab, Vector3.zero, Quaternion.identity) as GameObject;
						magicsFailed.transform.parent = this.magicsRenderer.transform;
						magicsFailed.transform.position = this.magicsRenderer.transform.position;
						this.magicsRenderer.SetActive(true);
					}
				}
			} else {
				// Set magics off
				this.magicsRenderer.SetActive(false);

				// Set Iris back into the waiting animation
				foreach (Transform child in this.irisRenderer.transform) {
					GameObject.Destroy(child.gameObject);
				}
				UnityEngine.Object irisMiaoPrefab = AssetDatabase.LoadAssetAtPath("Assets/Prefabs/cat_miao.prefab", typeof(GameObject));
				GameObject irisMiao = Instantiate(irisMiaoPrefab, Vector3.zero, Quaternion.identity) as GameObject;
				irisMiao.transform.parent = this.irisRenderer.transform;
				irisMiao.transform.position = this.irisRenderer.transform.position;
				this.currLevelState = LevelState.WaitingForAction;
			}
		}

		if (this.currLevelState == LevelState.SendingMagic) {
			foreach (Transform child in this.irisRenderer.transform) {
				GameObject.Destroy(child.gameObject);
			}
			UnityEngine.Object irisTwoHandsPrefab = AssetDatabase.LoadAssetAtPath("Assets/Prefabs/scratch_twohands.prefab", typeof(GameObject));
			GameObject irisTwoHands = Instantiate(irisTwoHandsPrefab, Vector3.zero, Quaternion.identity) as GameObject;
			irisTwoHands.transform.parent = this.irisRenderer.transform;
			irisTwoHands.transform.position = this.irisRenderer.transform.position;
			this.countdown = 3.0f;

			this.currLevelState = LevelState.SendingMagicInterim;
		}

		if (this.currLevelState == LevelState.SendingMagicInterim) {
			if (this.countdown > 0.0f) {
				this.countdown -= Time.deltaTime;

				if (this.countdown <= 2.0f) {
					// Show magics
					if (!this.magicsRenderer.activeInHierarchy) {
						foreach (Transform child in this.magicsRenderer.transform) {
							GameObject.Destroy(child.gameObject);
						}
						UnityEngine.Object magicsPrefab = AssetDatabase.LoadAssetAtPath("Assets/Prefabs/magics.prefab", typeof(GameObject));
						GameObject magics = Instantiate(magicsPrefab, Vector3.zero, Quaternion.identity) as GameObject;
						magics.transform.parent = this.magicsRenderer.transform;
						magics.transform.position = this.magicsRenderer.transform.position;
						this.magicsRenderer.SetActive(true);
					}
				}
			} else {
				// Set Magics off
				this.magicsRenderer.SetActive(false);

				// Set Iris to walking animation
				foreach (Transform child in this.irisRenderer.transform) {
					GameObject.Destroy(child.gameObject);
				}
				UnityEngine.Object irisWalkPrefab = AssetDatabase.LoadAssetAtPath("Assets/Prefabs/cat_walk.prefab", typeof(GameObject));
				GameObject irisWalk = Instantiate(irisWalkPrefab, Vector3.zero, Quaternion.identity) as GameObject;
				irisWalk.transform.parent = this.irisRenderer.transform;
				irisWalk.transform.position = this.irisRenderer.transform.position;

				// Remove enemy and item and set new enemy by picking a phrase
				foreach (Transform child in this.enemyAndItemRenderer.transform) {
					GameObject.Destroy(child.gameObject);
				}
				this.currPhrase = PhraseChooser();
				if (this.currPhrase != null) {
					UnityEngine.Object enemyAndItemPrefab = AssetDatabase.LoadAssetAtPath("Assets/Prefabs/" + this.currPhrase + ".prefab", typeof(GameObject));
					GameObject enemyAndItem = Instantiate(enemyAndItemPrefab, Vector3.zero, Quaternion.identity) as GameObject;
					enemyAndItem.transform.parent = this.enemyAndItemRenderer.transform;
					Vector3 enemyAndItemPosition = this.enemyAndItemRenderer.transform.position;
					enemyAndItemPosition.x += this.nextBackgroundRenderer.transform.position.x;
					this.enemyAndItemRenderer.transform.position = enemyAndItemPosition;
					enemyAndItem.transform.position = this.enemyAndItemRenderer.transform.position;
				}
				this.currLevelState = LevelState.Transitioning;
			}
		}

		if (this.currLevelState == LevelState.Transitioning) {
			float transitionSpeed = 0.1f;
			Vector3 currBackgroundPosition = this.currBackgroundRenderer.transform.position;
			currBackgroundPosition.x -= transitionSpeed;
			this.currBackgroundRenderer.transform.position = currBackgroundPosition;
			Vector3 nextBackgroundPosition = this.nextBackgroundRenderer.transform.position;
			nextBackgroundPosition.x -= transitionSpeed;
			this.nextBackgroundRenderer.transform.position = nextBackgroundPosition;
			Vector3 enemyAndItemPosition = this.enemyAndItemRenderer.transform.position;
			enemyAndItemPosition.x -= transitionSpeed;
			this.enemyAndItemRenderer.transform.position = enemyAndItemPosition;

			if (this.nextBackgroundRenderer.transform.position.x <= 0.0f) {
				if (this.currPhrase != null) {
					this.currLevelState = LevelState.Loading;
				} else {
					this.currLevelState = LevelState.Exiting;
				}
			}
		}

		if (this.currLevelState == LevelState.Exiting) {
			float transitionSpeed = 0.1f;
			Vector3 currIrisPosition = this.irisRenderer.transform.position;
			currIrisPosition.x += transitionSpeed;
			this.irisRenderer.transform.position = currIrisPosition;

			if (this.irisRenderer.transform.position.x >= this.enemyAndItemInitPosition.x) {
				this.irisRenderer.transform.position = this.irisInitPosition;
				this.enemyAndItemRenderer.transform.position = this.enemyAndItemInitPosition;
				this.currBackgroundRenderer.transform.position = this.currBackgroundInitPosition;
				this.nextBackgroundRenderer.transform.position = this.nextBackgroundInitPosition;
				LevelLoader.currLevel += 1;
				int maxLevel = Int32.Parse(this.phraseMetaData["maxPhraseLength"]) - 2;
				if (LevelLoader.currLevel <= maxLevel) {
					SceneManager.LoadScene("Main");
				} else {
					SceneManager.LoadScene("Menu");
				}
			}
		}
	}

	private void HintVideoEndReached(UnityEngine.Video.VideoPlayer vp) {
		vp.Stop();
		hintVideoRenderer.SetActive(false);
	}

	private string PhraseChooser() {
		if (this.unusedPhrases.Count != 0) {
			int randIndex = UnityEngine.Random.Range(0, this.unusedPhrases.Count);
			string randPhrase = this.unusedPhrases[randIndex];
			this.unusedPhrases.RemoveAt(randIndex);
			this.usedPhrases.Add(randPhrase);

			return randPhrase;
		} else {
			// We've run out of phrases
			return null;
		}
	}
}
