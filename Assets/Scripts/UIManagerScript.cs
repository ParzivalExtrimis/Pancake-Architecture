using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Backend.Models;
using UnityEngine.Networking;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;

public class UIManagerScript : MonoBehaviour {

    private static string URL = "https://corebackendfunctionapp.azurewebsites.net/api/StartCore";

    public InputField EmailInput;
    public InputField nameInput;
    public InputField passwordInput;
    public GameObject UiPanel;
    public GameObject LoadingPanel;
    public GameObject Display;
    public Text displayText;
    public float WaitTime = 3.0f;

    private string email;
    private string name;
    private string password;
    private DurableTask task;
    private Status status;
    private Output output;
    public void clickedEventHandler() {
        name = nameInput.text;
        password = passwordInput.text;
        email = EmailInput.text;

        print($"Name: {name}, \n Password: {password} \n Email: {email}");

        var user = new User {
            Username = name,
            Email = email,
            Password = password
        };
        UiPanel.SetActive(false);
        LoadingPanel.SetActive(true);
        StartCoroutine(CheckState(user));
    }
    public IEnumerator SendRequest(User user) {
        if (user != null) {
            var request = new UnityWebRequest(URL, "POST");

            var serializer = new DataContractJsonSerializer(typeof(User));
            byte[] jsonBytes;
            string jsonString;

            // create a stream to hold the serialized data
            using (var stream = new MemoryStream()) {
                serializer.WriteObject(stream, user);

                jsonBytes = stream.ToArray();
                jsonString = Encoding.UTF8.GetString(jsonBytes);
            }
            Debug.Log($"User JSON: {jsonString}");

            // Set the request headers
            request.SetRequestHeader("Content-Type", "application/json");
            request.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonBytes);
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();

            // Send the request and wait for a response
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success) {
                Debug.Log(request.error);
                request.Dispose();
            }
            else {
                Debug.Log("Request sent successfully");
                var json = request.downloadHandler.text;
                Debug.Log(json);

                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json))) {
                    var taskSerializer = new DataContractJsonSerializer(typeof(DurableTask));
                    task = taskSerializer.ReadObject(stream) as DurableTask;
                }

                Debug.Log($"Task: \n {task.id}");
                request.Dispose();
            }
        }
        else {
           Debug.Log("User Model was null.");
        }
       
    }

    public IEnumerator CheckState(User user) {
        yield return StartCoroutine(SendRequest(user));
        Debug.Log("Request Sent. Response Recieved.");
        yield return StartCoroutine(GetFunctionState(task));
        Debug.Log("Core execution complete.");
    }

    public IEnumerator GetFunctionState(DurableTask task) {
        if (task != null) {
            var request = new UnityWebRequest(task.statusQueryGetUri, "GET");
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();

            // Send the request and wait for a response
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success) {
                Debug.Log(request.error);
                request.Dispose();
            }
            else {
                Debug.Log("( Function State Query ) Request sent successfully");
                var json = request.downloadHandler.text;
                Debug.Log(json);

                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json))) {
                    var statusSerializer = new DataContractJsonSerializer(typeof(Status));
                    status = statusSerializer.ReadObject(stream) as Status;
                }

                Debug.Log($"Task: \n {status.runtimeStatus}");
                while(status.runtimeStatus == "Running" || status.runtimeStatus == "Pending") {
                    request.Dispose();
                    yield return new WaitForSeconds(WaitTime);
                    StartCoroutine(GetFunctionState(task));
                }
                if(status.runtimeStatus == "Failed") {
                    request.Dispose();
                    Debug.Log("Something went wrong. Try again.");
                    yield return null;
                }
                if(status.runtimeStatus == "Completed") {
                    Debug.Log($"Output recieved: {status.output}");

                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(status.output))) {
                        var outputSerializer = new DataContractJsonSerializer(typeof(Output));
                        output = outputSerializer.ReadObject(stream) as Output;
                    }

                    Debug.Log($"Output Location: \n {output.location}");

                    LoadingPanel.SetActive(false);
                    displayText.text = format(output);
                    Display.SetActive(true);
                }

                request.Dispose();
            }
        }
        else {
            Debug.Log("Task model was null.");
        }
    }

    private string format(Output output) {
        string formatted = "";
        if (output != null) {
            formatted += "Status: ";
            formatted += output.state;
            formatted += "\n";
            formatted += "Location: ";
            formatted += output.location;
            formatted += "\n\n";

            formatted += "Batch: ";
            formatted += output.data.batch;
            formatted += "\n";
            formatted += "Grade: ";
            formatted += output.data.grade;
            formatted += "\n";
            formatted += "School: ";
            formatted += output.data.school;
            formatted += "\n\n";

            formatted += "Subjects: {";
            foreach (var sub in output.data.subjects) {
                formatted += sub + ", ";
            }
            formatted += "}\n";

            formatted += "Chapters: {";
            foreach (var chap in output.data.chapters) {
                formatted += chap + ", ";
            }
            formatted += "}\n";

            formatted += "Content: {";
            foreach (var con in output.data.content) {
                formatted += con + ", ";
            }
            formatted += "}\n";
        }
        return formatted;
    }
}
