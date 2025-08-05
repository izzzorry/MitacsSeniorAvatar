using System;
using System.Collections.Generic;
using Firebase.Database;
using Firebase.Extensions;
using TMPro;
using UnityEngine;

public class ConnectionManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField displayNetworkInput;
    [SerializeField] private TMP_Text statusText;

    private string currentNetwork;
    private bool networkReady;
    private DatabaseReference currentRoomRef;

    private void OnEnable()
    {
        FirebaseInit.OnFirebaseReady += OnFirebaseReady;
    }

    private void OnDisable()
    {
        FirebaseInit.OnFirebaseReady -= OnFirebaseReady;
        if (currentRoomRef != null)
            currentRoomRef.ValueChanged -= OnCurrentRoomChanged;
    }

    private void OnFirebaseReady()
    {
        UpdateStatus("Firebase listo, suscribiéndome a currentRoom...");
        SubscribeToCurrentRoom();
    }

    private void SubscribeToCurrentRoom()
    {
        if (FirebaseInit.RootRef == null)
        {
            UpdateStatus("RootRef es null. Firebase no está listo.");
            return;
        }

        // Apunta al nodo “/currentRoom”
        currentRoomRef = FirebaseInit.RootRef.Child("currentRoom");
        currentRoomRef.ValueChanged += OnCurrentRoomChanged;

        // Lectura inicial con debug
        currentRoomRef.GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("Error lectura inicial: " + task.Exception);
                UpdateStatus("Error leyendo currentRoom.");
                return;
            }

            var snap = task.Result;
            Debug.Log($"[DEBUG currentRoom] Exists={snap.Exists}, Value='{snap.Value}', RawJson='{snap.GetRawJsonValue()}'");

            if (snap.Exists && snap.Value != null)
            {
                currentNetwork = snap.Value.ToString();
                FinalizeSubscription();
            }
            else
            {
                UpdateStatus("No hay currentRoom. Creando uno por defecto...");
                string defaultRoom = "SalaAuto" + UnityEngine.Random.Range(100, 999);
                currentRoomRef.SetValueAsync(defaultRoom).ContinueWithOnMainThread(t2 =>
                {
                    if (t2.IsFaulted)
                    {
                        Debug.LogError("No pudo crear defaultRoom: " + t2.Exception);
                        UpdateStatus("Error inicializando sala.");
                        return;
                    }
                    currentNetwork = defaultRoom;
                    FinalizeSubscription();
                });
            }
        });
    }

    private void OnCurrentRoomChanged(object sender, ValueChangedEventArgs e)
    {
        if (e.DatabaseError != null)
        {
            Debug.LogError("Listener error: " + e.DatabaseError.Message);
            UpdateStatus("Error escuchando currentRoom.");
            return;
        }
        if (e.Snapshot.Exists && e.Snapshot.Value != null)
        {
            currentNetwork = e.Snapshot.Value.ToString();
            FinalizeSubscription();
        }
    }

    private void FinalizeSubscription()
    {
        networkReady = true;
        RefreshUI();
        UpdateStatus($"Sala actual: {currentNetwork}");
    }

    private void RefreshUI()
    {
        if (displayNetworkInput != null)
        {
            displayNetworkInput.text = currentNetwork;
            displayNetworkInput.interactable = false;
        }
    }

    private void UpdateStatus(string msg)
    {
        statusText?.SetText(msg);
        Debug.Log("[ConnectionManager] " + msg);
    }

    public void CreateRoom()
    {
        if (!FirebaseInit.IsReady)
        {
            UpdateStatus("Firebase aún no listo.");
            return;
        }
        string newRoom = Guid.NewGuid().ToString("N").Substring(0, 8);
        var r = FirebaseInit.RootRef.Child("rooms").Child(newRoom);
        var data = new Dictionary<string, object>
        {
            {"createdAt", ServerValue.Timestamp},
            {"owner", FirebaseInit.UserId},
            {"participants", new Dictionary<string,object>()}
        };
        r.SetValueAsync(data).ContinueWithOnMainThread(t =>
        {
            if (t.IsFaulted)
            {
                Debug.LogError("Error creando sala: " + t.Exception);
                UpdateStatus("Error creando sala.");
                return;
            }
            UpdateStatus($"Sala creada: {newRoom}. Actualizando currentRoom...");
            SetNetwork(newRoom, runSession: true, create: true);
        });
    }

    public void JoinRoom()
    {
        if (!networkReady)
        {
            UpdateStatus("No listo para unirse.");
            return;
        }
        UpdateStatus($"Uniéndose a sala: {currentNetwork}");
        NetworkManager.Instance.JoinSession(currentNetwork);
    }

    public void SetNetwork(string room, bool runSession=false, bool create=false)
    {
        if (!FirebaseInit.IsReady)
        {
            UpdateStatus("Firebase aún no listo.");
            return;
        }
        FirebaseInit.RootRef.Child("currentRoom").SetValueAsync(room)
            .ContinueWithOnMainThread(t =>
        {
            if (t.IsFaulted)
            {
                Debug.LogError("Error actualizando currentRoom: " + t.Exception);
                UpdateStatus("Error actualizando sala.");
                return;
            }
            currentNetwork = room;
            FinalizeSubscription();
            if (runSession)
            {
                if (create)
                    NetworkManager.Instance.CreateSession(room);
                else
                    NetworkManager.Instance.JoinSession(room);
            }
        });
    }
}
