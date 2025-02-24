using UnityEngine;
using TMPro;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(ExplodedView))]
public class ExplodedViewEditor : Editor
{
    private int partIndex = 0;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        ExplodedView explodedViewScript = (ExplodedView)target;

        // Options pour tester SelectPart et DeselectPart
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Test SelectPart", EditorStyles.boldLabel);
        partIndex = EditorGUILayout.IntField("Part Index", partIndex);

        if (GUILayout.Button("Check SelectPart"))
        {
            explodedViewScript.SelectPart(partIndex);
        }

        if (GUILayout.Button("Check DeselectPart"))
        {
            explodedViewScript.DeselectPart(partIndex);
        }
    }
}
#endif

public class ExplodedView : MonoBehaviour
{
#if UNITY_EDITOR
    [SerializeField] private bool DEBUG_VIEW = true;
#endif

    [System.Serializable]
    public class Part
    {
        public Transform partTransform; // Reference to the part's Transform
        public Vector3 finalPosition;  // Final position in exploded view
        public Vector3 offset;         // Offset when selected
        public string title;     // Title of the part
        public string desc;     // Description of the part
        public Sprite image;     // Image of the part

        public Vector3 InitialPosition { get; set; }
        public bool IsSelected { get; set; } = false;
    }

    [SerializeField] private Part[] parts = new Part[4]; // Array to hold the 4 parts

    [SerializeField] private GameObject infos;
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI desc;
    [SerializeField] private GameObject imgGO;
    [SerializeField] private GameObject[] arrows;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] audioClips;


    public bool IsExplodedView { get; private set; } = false;

    public float transitionSpeed = 2f; // Speed for smooth transitions

    private bool isToggling = false;
    private bool anyPartSelectedFlag = false; // Tracks selection state

    private bool firstPartSelected = false;

    private void Awake()
    {
        UpdateInfosUI();
    }

    public void RuntimeShowExplodedView(int partIndex)
    {
        if (IsExplodedView)
        {
            if (!firstPartSelected)
            {
                firstPartSelected = true;
                GameManager.Instance.mainStateMachine.IncrementState();
            }
            if (parts[partIndex].IsSelected) DeselectPart(partIndex);
            else SelectPart(partIndex);
        }
        else ToggleExplodedView();
    }

    public void ToggleExplodedView()
    {
        if (anyPartSelectedFlag)
        {
#if UNITY_EDITOR
            if (DEBUG_VIEW) Debug.Log("Cannot toggle exploded view while a part is selected.");
#endif
            return;
        }

        if (isToggling) return;

        IsExplodedView = !IsExplodedView;
        isToggling = true;

        if (!IsExplodedView)
        {
            UpdateInfosUI();
        }

        StartCoroutine(MoveAllPartsSmoothly());
    }

    public void SelectPart(int partIndex)
    {
        if (!IsExplodedView)
        {
#if UNITY_EDITOR
            if (DEBUG_VIEW) Debug.LogError("Cannot select part when not in exploded view mode.");
#endif
            return;
        }

        if (partIndex < 0 || partIndex >= parts.Length)
        {
#if UNITY_EDITOR
            if (DEBUG_VIEW) Debug.LogError("Invalid part index.");
#endif
            return;
        }

        Part part = parts[partIndex];

        if (part.IsSelected)
        {
#if UNITY_EDITOR
            if (DEBUG_VIEW) Debug.LogError("Part already selected.");
#endif
            return;
        }

        if (anyPartSelectedFlag)
        {
            DeselectAllParts();
        }

        part.IsSelected = true;
        anyPartSelectedFlag = true;

        UpdateInfosUI(part, partIndex);
        PlayTtsPart(partIndex);
        Vector3 offsetPosition = part.partTransform.position + part.offset;
        StartCoroutine(MovePartSmoothly(part.partTransform, offsetPosition));
    }

    private void PlayTtsPart(int partIndex)
    {
        audioSource.clip = audioClips[partIndex];
        audioSource.Play();
    }
    private void DeselectAllParts()
    {
        int i = 0;
        while (i < parts.Length)
        {
            if (parts[i].IsSelected)
            {
                DeselectPart(i);
                i = parts.Length;
            }
            i++;
        }
    }

    private void UpdateInfosUI(Part part = null, int partIndex = 0)
    {
        if (anyPartSelectedFlag && (part != null))
        {
            title.text = part.title;
            desc.text = part.desc;
            imgGO.SetActive(part.image != null);
            imgGO.GetComponent<Image>().sprite = part.image;
            for(int i = 0; i < arrows.Length; i++)
            {
                arrows[i].SetActive(i == partIndex);
            }
            infos.SetActive(true);
        }
        else
        {
            infos.SetActive(false);
        }
    }

    public void DeselectPart(int partIndex)
    {
        if (partIndex < 0 || partIndex >= parts.Length)
        {
#if UNITY_EDITOR
            if (DEBUG_VIEW) Debug.LogError("Invalid part index.");
#endif
            return;
        }

        Part part = parts[partIndex];

        if (!part.IsSelected)
        {
#if UNITY_EDITOR
            if (DEBUG_VIEW) Debug.LogError("Part not selected.");
#endif
            return;
        }

        part.IsSelected = false;

        anyPartSelectedFlag = false;

        UpdateInfosUI();

        Vector3 targetPosition = part.finalPosition + transform.position;
        StartCoroutine(MovePartSmoothly(part.partTransform, targetPosition));
    }

    private System.Collections.IEnumerator MovePartSmoothly(Transform partTransform, Vector3 targetPosition)
    {
        float elapsedTime = 0f;
        float transitionDuration = 1f / transitionSpeed;
        Vector3 initialPosition = partTransform.position;

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / transitionDuration);
            partTransform.position = Vector3.Lerp(initialPosition, targetPosition, t);
            yield return null;
        }
        partTransform.position = targetPosition; // Ensure final position is precise
    }

    private System.Collections.IEnumerator MoveAllPartsSmoothly()
    {
        Vector3[] initialPositions = new Vector3[parts.Length];
        Vector3[] targetPositions = new Vector3[parts.Length];
        float transitionDuration = 1f / transitionSpeed;

        for (int i = 0; i < parts.Length; i++)
        {
            initialPositions[i] = parts[i].partTransform.position;
            if (IsExplodedView)
            {
                targetPositions[i] = parts[i].finalPosition + transform.position;
                parts[i].InitialPosition = parts[i].partTransform.position;
            }
            else
            {
                targetPositions[i] = parts[i].InitialPosition;
            }
            
        }

        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / transitionDuration);

            for (int i = 0; i < parts.Length; i++)
            {
                parts[i].partTransform.position = Vector3.Lerp(initialPositions[i], targetPositions[i], t);
            }
            yield return null;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            parts[i].partTransform.position = targetPositions[i];
        }

        isToggling = false;
    }
}
