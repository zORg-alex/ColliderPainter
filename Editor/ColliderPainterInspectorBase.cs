
using UnityEditor;
using UnityEngine;

public abstract class ColliderPainterInspectorBase : Editor
{
	protected static MeshCollider painterMeshCollider;
	protected MeshFilter targetMeshFilter;
	protected ColliderPainterStateScript targetScript;
	protected bool isEditing;
	public static int currentlyEditedGroup;
	int lastRenderedFrame;


	protected virtual void OnEnable()
	{
		targetScript = (ColliderPainterStateScript)target;
		if (targetScript.ForceEdit)
			StartEditing(0);

		SubscribeToThings();
	}

	private void SubscribeToThings()
	{
		SceneView.duringSceneGui += SceneView_duringSceneGui;
		Selection.selectionChanged += UnsubscribeIfSelectionChanged;
		Undo.undoRedoPerformed += UnsubscribeIfSelectionChanged;
	}

	private void UnsubscribeIfSelectionChanged()
	{
		if (Selection.activeGameObject != targetScript.gameObject)
			UnsubscribeFromThings();
	}

	private void UnsubscribeFromThings()
	{
		SceneView.duringSceneGui -= SceneView_duringSceneGui;
		Selection.selectionChanged -= UnsubscribeIfSelectionChanged;
		Undo.undoRedoPerformed -= UnsubscribeIfSelectionChanged;
	}

	protected void StartEditing(int i)
	{
		isEditing = true;
		currentlyEditedGroup = i;
		targetScript.FixMissingGroups();

		targetScript.ForceEdit = false;
		targetScript.BeforeStartEditing();
		targetMeshFilter = targetScript.GetComponent<MeshFilter>();

		SetUpPaintMeshCollider();

		painterMeshCollider.transform.SetParent(targetScript.transform);
		painterMeshCollider.transform.Reset();
		painterMeshCollider.sharedMesh = targetMeshFilter.sharedMesh;
	}

	private static void SetUpPaintMeshCollider()
	{
		if (!painterMeshCollider)
		{
			var invisibleObject = new GameObject("ColliderPainterDummy", typeof(MeshCollider));
			invisibleObject.hideFlags = HideFlags.HideAndDontSave;
			painterMeshCollider = invisibleObject.GetComponent<MeshCollider>();
			painterMeshCollider.convex = false;
		}
	}

	protected virtual void OnDisable()
	{
		if (target)
			StopEditing();
		UnsubscribeFromThings();
		SceneView.RepaintAll();
	}

	protected void StopEditing()
	{
		isEditing = false;
		currentlyEditedGroup = -1;
		targetScript.BeforeStopEditing();
		Repaint();
		if (painterMeshCollider)
			DestroyImmediate(painterMeshCollider.gameObject);
	}

	private void SceneView_duringSceneGui(SceneView obj)
	{
		if (!targetScript) { UnsubscribeFromThings(); return; } //Fix for exceptions

		Handles.BeginGUI();
		DrawSceneGUI();
		Handles.EndGUI();

		DrawSceneView();

		if (Event.current.type == EventType.Layout && lastRenderedFrame != Time.renderedFrameCount)
		{
			DrawGraphics();
			lastRenderedFrame = Time.renderedFrameCount;
		}
	}

	public abstract void DrawSceneView();

	public abstract void DrawSceneGUI();

	public override void OnInspectorGUI()
	{
		OnGUI();
	}
	protected void DefaultInspector()
	{
		base.OnInspectorGUI();
	}
	public abstract void OnGUI();

	public abstract void DrawGraphics();
}
