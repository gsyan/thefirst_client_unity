//------------------------------------------------------------------------------
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

public class TestApi : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(TestApiCalls());
    }

    private IEnumerator TestApiCalls()
    {
        NetworkManager.Instance.Register("user1@example.com", "mySecurePassword");
        yield return new WaitForSeconds(1f);

        NetworkManager.Instance.Login("user1@example.com", "mySecurePassword");
        yield return new WaitForSeconds(1f);

        NetworkManager.Instance.CreateCharacter("Hero1");
        yield return new WaitForSeconds(1f);

        yield return StartCoroutine(RunAsync(() => NetworkManager.Instance.RefreshAccessTokenAsync()));

        NetworkManager.Instance.Logout();
    }

    private IEnumerator RunAsync(System.Func<Task> taskFunc)
    {
        Task task = taskFunc();
        while (!task.IsCompleted)
        {
            yield return null;
        }
        if (task.IsFaulted)
        {
            Debug.LogError($"Task failed: {task.Exception?.InnerException?.Message}");
        }
    }
}