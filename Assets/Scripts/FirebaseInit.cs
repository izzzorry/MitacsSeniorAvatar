using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;

public class FirebaseInit : MonoBehaviour
{
    public static FirebaseAuth Auth;
    public static DatabaseReference RootRef;
    public static string UserId;
    public static bool IsReady { get; private set; }

    public static event System.Action OnFirebaseReady;

    [Header("Opcional: si el google-services.json no se carga, pon aquí tu URL de Realtime DB")]
    [SerializeField] private string fallbackDatabaseUrl = "https://avatarsvr-ddb1c-default-rtdb.firebaseio.com/"; // ej: "https://<tu-proyecto>-default-rtdb.firebaseio.com/"

    private void Awake()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result != DependencyStatus.Available)
            {
                Debug.LogError("Firebase no está disponible: " + task.Result);
                return;
            }

            // Usa la instancia por defecto
            var app = FirebaseApp.DefaultInstance;

            // Inicializa Auth
            Auth = FirebaseAuth.DefaultInstance;

            // Inicializa Realtime Database; si se proporcionó fallback, úsalo explícitamente
            if (!string.IsNullOrEmpty(fallbackDatabaseUrl))
            {
                var db = FirebaseDatabase.GetInstance(app, fallbackDatabaseUrl);
                RootRef = db.RootReference;
            }
            else
            {
                RootRef = FirebaseDatabase.DefaultInstance.RootReference;
            }

            // Autenticación anónima
            Auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(authTask =>
            {
                if (authTask.IsCanceled || authTask.IsFaulted)
                {
                    Debug.LogError("Error autenticando anónimamente: " + authTask.Exception);
                    return;
                }

                UserId = Auth.CurrentUser.UserId;
                Debug.Log($"Firebase listo. Usuario anónimo con UID: {UserId}");

                IsReady = true;
                OnFirebaseReady?.Invoke();
            });
        });
    }
}
