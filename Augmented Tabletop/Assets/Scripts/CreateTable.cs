﻿namespace GoogleARCore.Examples.HelloAR
{
	using System.Collections.Generic;
	using Firebase.Database;
	using GoogleARCore;
	using GoogleARCore.CrossPlatform;
	using GoogleARCore.Examples.Common;
	using UnityEngine;
	using UnityEngine.EventSystems;
	using UnityEngine.SceneManagement;

#if UNITY_EDITOR
	// Set up touch input propagation while using Instant Preview in the editor.
	using Input = InstantPreviewInput;
#endif

	/// <summary>
	/// Controls the HelloAR example.
	/// </summary>
	public class CreateTable : MonoBehaviour
	{
		/// <summary>
		/// The first-person camera being used to render the passthrough camera image (i.e. AR
		/// background).
		/// </summary>
		public Camera FirstPersonCamera;

		/// <summary>
		/// A prefab to place when a raycast from a user touch hits a vertical plane.
		/// </summary>
		public GameObject GameObjectVerticalPlanePrefab;

		/// <summary>
		/// A prefab to place when a raycast from a user touch hits a horizontal plane.
		/// </summary>
		public GameObject GameObjectHorizontalPlanePrefab;

		/// <summary>
		/// A prefab to place when a raycast from a user touch hits a feature point.
		/// </summary>
		public GameObject GameObjectPointPrefab;

		/// <summary>
		/// The rotation in degrees need to apply to prefab when it is placed.
		/// </summary>
		private const float k_PrefabRotation = 180.0f;

		/// <summary>
		/// True if the app is in the process of quitting due to an ARCore connection error,
		/// otherwise false.
		/// </summary>
		private bool m_IsQuitting = false;

		/// <summary>
		/// The Unity Awake() method.
		/// </summary>
		public void Awake()
		{
			// Enable ARCore to target 60fps camera capture frame rate on supported devices.
			// Note, Application.targetFrameRate is ignored when QualitySettings.vSyncCount != 0.
			Application.targetFrameRate = 60;
		}

		/// <summary>
		/// The Unity Update() method.
		/// </summary>
		public void Update()
		{
			_UpdateApplicationLifecycle();

			// If the player has not touched the screen, we are done with this update.
			Touch touch;
			if (Input.touchCount < 1 || (touch = Input.GetTouch(0)).phase != TouchPhase.Began)
			{
				return;
			}

			// Should not handle input if the player is pointing on UI.
			if (EventSystem.current.IsPointerOverGameObject(touch.fingerId))
			{
				return;
			}

			// Raycast against the location the player touched to search for planes.
			TrackableHit hit;
			TrackableHitFlags raycastFilter = TrackableHitFlags.PlaneWithinPolygon |
				TrackableHitFlags.FeaturePointWithSurfaceNormal;

			if (Frame.Raycast(touch.position.x, touch.position.y, raycastFilter, out hit))
			{
				// Use hit pose and camera pose to check if hittest is from the
				// back of the plane, if it is, no need to create the anchor.
				if ((hit.Trackable is DetectedPlane) &&
					Vector3.Dot(FirstPersonCamera.transform.position - hit.Pose.position,
						hit.Pose.rotation * Vector3.up) < 0)
				{
					Debug.Log("Hit at back of the current DetectedPlane");
				} else
				{
					// Choose the prefab based on the Trackable that got hit.
					GameObject prefab;
					if (hit.Trackable is FeaturePoint)
					{
						prefab = GameObjectPointPrefab;
					} else if (hit.Trackable is DetectedPlane)
					{
						DetectedPlane detectedPlane = hit.Trackable as DetectedPlane;
						if (detectedPlane.PlaneType == DetectedPlaneType.Vertical)
						{
							prefab = GameObjectVerticalPlanePrefab;
						} else
						{
							prefab = GameObjectHorizontalPlanePrefab;
						}
					} else
					{
						prefab = GameObjectHorizontalPlanePrefab;
					}

					// Instantiate prefab at the hit pose.
					var gameObject = Instantiate(prefab, hit.Pose.position, hit.Pose.rotation);

					// Compensate for the hitPose rotation facing away from the raycast (i.e.
					// camera).
					gameObject.transform.Rotate(0, k_PrefabRotation, 0, Space.Self);

					// Create an anchor to allow ARCore to track the hitpoint as understanding of
					// the physical world evolves.
					var anchor = hit.Trackable.CreateAnchor(hit.Pose);

					// Make game object a child of the anchor.
					gameObject.transform.parent = anchor.transform;

					_DoUploadAndMoveOn(anchor);
				}
			}
		}

		/// <summary>
		/// Check and update the application lifecycle.
		/// </summary>
		private void _UpdateApplicationLifecycle()
		{
			// Exit the app when the 'back' button is pressed.
			if (Input.GetKey(KeyCode.Escape))
			{
				Application.Quit();
			}

			// Only allow the screen to sleep when not tracking.
			if (Session.Status != SessionStatus.Tracking)
			{
				Screen.sleepTimeout = SleepTimeout.SystemSetting;
			} else
			{
				Screen.sleepTimeout = SleepTimeout.NeverSleep;
			}

			if (m_IsQuitting)
			{
				return;
			}

			// Quit if ARCore was unable to connect and give Unity some time for the toast to
			// appear.
			if (Session.Status == SessionStatus.ErrorPermissionNotGranted)
			{
				_ShowAndroidToastMessage("Camera permission is needed to run this application.");
				m_IsQuitting = true;
				Invoke("_DoQuit", 0.5f);
			} else if (Session.Status.IsError())
			{
				_ShowAndroidToastMessage(
					"ARCore encountered a problem connecting.  Please start the app again.");
				m_IsQuitting = true;
				Invoke("_DoQuit", 0.5f);
			}
		}

		private void _DoUploadAndMoveOn(Anchor anchor)
		{
			_ShowAndroidToastMessage("Saving anchor " + anchor.ToString());
			XPSession.CreateCloudAnchor(anchor).ThenAction(result => {
				if (result.Response != CloudServiceResponse.Success)
				{
					Debug.Log(string.Format("Failed to host Cloud Anchor: {0}", result.Response));
					_ShowAndroidToastMessage(string.Format("Failed to host Cloud Anchor: {0}", result.Response));
					return;
				}
				_ShowAndroidToastMessage(string.Format(
					"Cloud Anchor {0} was created and saved.", result.Anchor.CloudId));

				Debug.Log(string.Format(
					"Cloud Anchor {0} was created and saved.", result.Anchor.CloudId));

				string cloudID = result.Anchor.CloudId;
				FirebaseDatabase database = FirebaseDatabase.DefaultInstance;
				int tablenum = Random.Range(100000, 999999);
				DatabaseReference row = database.GetReference(tablenum.ToString());
				string json = "{\"cloudID\":\"" + cloudID + "\""
				+ ",\"array\":["
				+ "{\"data\":\"floor\",\"position\":[0,0,0],\"rotation\":[0,90,0]},"
				+ "{\"data\":\"floor\",\"position\":[1,0,0],\"rotation\":[0,90,0]},"
				+ "{\"data\":\"floor\",\"position\":[2,0,0],\"rotation\":[0,90,0]},"
				+ "{\"data\":\"floor\",\"position\":[3,0,0],\"rotation\":[0,90,0]},"
				+ "{\"data\":\"floor\",\"position\":[0,0,1],\"rotation\":[0,90,0]},"
				+ "{\"data\":\"floor\",\"position\":[1,0,1],\"rotation\":[0,90,0]},"
				+ "{\"data\":\"floor\",\"position\":[2,0,1],\"rotation\":[0,90,0]},"
				+ "{\"data\":\"floor\",\"position\":[3,0,1],\"rotation\":[0,90,0]},"
				+ "{\"data\":\"floor\",\"position\":[0,0,2],\"rotation\":[0,90,0]},"
				+ "{\"data\":\"floor\",\"position\":[1,0,2],\"rotation\":[0,90,0]},"
				+ "{\"data\":\"floor\",\"position\":[2,0,2],\"rotation\":[0,90,0]},"
				+ "{\"data\":\"floor\",\"position\":[3,0,2],\"rotation\":[0,90,0]},"
				+ "{\"data\":\"wall\",\"position\":[0,1,-1],\"rotation\":[0,-90,0]},"
				+ "{\"data\":\"wall\",\"position\":[1,1,-1],\"rotation\":[0,-90,0]},"
				+ "{\"data\":\"wall\",\"position\":[2,1,-1],\"rotation\":[0,-90,0]},"
				+ "{\"data\":\"wall\",\"position\":[3,1,-1],\"rotation\":[0,-90,0]},"
				+ "{\"data\":\"wall\",\"position\":[0,1,3],\"rotation\":[0,90,0]},"
				+ "{\"data\":\"wall\",\"position\":[1,1,3],\"rotation\":[0,90,0]},"
				+ "{\"data\":\"wall\",\"position\":[2,1,3],\"rotation\":[0,90,0]},"
				+ "{\"data\":\"wall\",\"position\":[3,1,3],\"rotation\":[0,90,0]},"
				+ "{\"data\":\"wall\",\"position\":[-1,1,0],\"rotation\":[0,0,0]},"
				+ "{\"data\":\"wall\",\"position\":[-1,1,1],\"rotation\":[0,0,0]},"
				+ "{\"data\":\"wall\",\"position\":[-1,1,2],\"rotation\":[0,0,0]},"
				+ "{\"data\":\"wall\",\"position\":[4,1,0],\"rotation\":[0,180,0]},"
				+ "{\"data\":\"wall\",\"position\":[4,1,1],\"rotation\":[0,180,0]},"
				+ "{\"data\":\"wall\",\"position\":[4,1,2],\"rotation\":[0,180,0]}"
				+ "]}";

				_ShowAndroidToastMessage("saving cloud data");
				row.SetRawJsonValueAsync(json).ContinueWith(task => {
					if (task.IsFaulted)
					{
						_ShowAndroidToastMessage("Failed, " + task.Exception.ToString());
					} else if (task.IsCompleted)
					{
						Table.tableNumber = tablenum;
						SceneManager.LoadScene("Table");
					} else
					{
						_ShowAndroidToastMessage("?, " + task.Status);
					}
				});
			});
		}

		/// <summary>
		/// Actually quit the application.
		/// </summary>
		private void _DoQuit()
		{
			Application.Quit();
		}

		/// <summary>
		/// Show an Android toast message.
		/// </summary>
		/// <param name="message">Message string to show in the toast.</param>
		private void _ShowAndroidToastMessage(string message)
		{
			AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
			AndroidJavaObject unityActivity =
				unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

			if (unityActivity != null)
			{
				AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast");
				unityActivity.Call("runOnUiThread", new AndroidJavaRunnable(() => {
					AndroidJavaObject toastObject =
						toastClass.CallStatic<AndroidJavaObject>(
							"makeText", unityActivity, message, 0);
					toastObject.Call("show");
				}));
			}
		}
	}
}
