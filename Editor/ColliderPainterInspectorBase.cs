﻿
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public abstract class ColliderPainterInspectorBase : Editor
{
	protected static MeshCollider painterMeshCollider;
	protected MeshFilter targetMeshFilter;
	protected ColliderPainter targetScript;
	protected bool isEditing;
	public static int currentlyEditedGroup;
	int lastRenderedFrame;
	private Transform[] transformsToRemove;
	private static string DefaultAssetPath = "Assets/Generated/";

	protected virtual void OnEnable()
	{
		targetScript = (ColliderPainter)target;
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

		painterMeshCollider.transform.PositionAsChildOfNonRigidBody(targetScript.transform, out transformsToRemove);
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
		targetScript.RefreshMeshColliders();
		RefreshAsset();
		Repaint();
		if (painterMeshCollider)
			transformsToRemove.DestroyImmediate();
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



	private List<Mesh> GetAssetMeshes()
	{
		var list = new List<Mesh>();
		foreach (var a in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(targetScript.asset)))
		{
			if (a is Mesh m)
				list.Add(m);
		}
		return list;
	}

	private bool IsPartOfPrefab()
	{
		//In scene
		if (PrefabUtility.GetCorrespondingObjectFromSource(target) is UnityEngine.Object asset)
			return true;
		//In prefab scene
		if (string.IsNullOrEmpty(targetScript.gameObject.scene.path) && !string.IsNullOrEmpty(targetScript.gameObject.scene.name))
			return true;
		return false;
	}

	private IList<Mesh> GetAllMeshes()
	{
		var list = new List<Mesh>();
		var prefabRoot = GetPrefabRootObject();
		if (prefabRoot == null)
			return targetScript.meshes;

		var painters = prefabRoot.GetComponentsInChildren<ColliderPainter>();
		foreach (var p in painters)
		{
			list.AddRange(p.meshes);
		}
		return list;
	}

	private GameObject GetPrefabRootObject()
	{
		//In scene
		if (PrefabUtility.GetCorrespondingObjectFromSource(target) is UnityEngine.Object asset)
			return asset is GameObject go ? go : ((Component)asset).gameObject;
		//In prefab scene
		if (string.IsNullOrEmpty(targetScript.gameObject.scene.path) && !string.IsNullOrEmpty(targetScript.gameObject.scene.name) &&
			targetScript.gameObject.scene.GetRootGameObjects() is GameObject[] obj && obj.Length == 1)
			return obj[0];
		return null;
	}

	private void RefreshAsset()
	{
		if (!targetScript.asset)
		{
			//Save meshes on a prefab or generate a new one
			if (PrefabUtility.GetCorrespondingObjectFromSource(target) is UnityEngine.Object asset)
			{
				targetScript.asset = asset;
			}
			else
			{
				targetScript.asset = CreateInstance<DummyAsset>();
				string name = GUID.Generate().ToString();
				targetScript.asset.name = name;
				var path = DefaultAssetPath + name + ".asset";
				if (!AssetDatabase.IsValidFolder(DefaultAssetPath))
					AssetDatabase.CreateFolder("Assets", "Generated");
				AssetDatabase.CreateAsset(targetScript.asset, path);
			}
			foreach (var m in targetScript.meshes)
			{
				if (m)
					AssetDatabase.AddObjectToAsset(m, AssetDatabase.GetAssetPath(targetScript.asset));
			}
			return;
		}
		else
		{
			string path = AssetDatabase.GetAssetPath(targetScript.asset);
			if (path.Length > DefaultAssetPath.Length && path.Substring(0, DefaultAssetPath.Length) == DefaultAssetPath &&
				PrefabUtility.GetCorrespondingObjectFromSource(target) is UnityEngine.Object asset)
			{
				AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(targetScript.asset));
				targetScript.asset = asset;
			}
		}


		var existingMeshes = GetAllMeshes();
		foreach (var m in existingMeshes)
		{
			if (!targetScript.meshes.Contains(m))
			{
				AssetDatabase.RemoveObjectFromAsset(m);
			}
		}
		foreach (var m in targetScript.meshes)
		{
			if (!existingMeshes.Contains(m) && m)
				AssetDatabase.AddObjectToAsset(m, targetScript.asset);
		}
	}
}
