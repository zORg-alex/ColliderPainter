using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[ExecuteInEditMode]
public class ColliderPainterStateScript : MonoBehaviour
{
	[SerializeField]
	private List<TriangleGroup> groups = new List<TriangleGroup>();
	[SerializeField]
	private List<Mesh> meshes = new List<Mesh>();

	[NonSerialized]
	public bool ForceEdit;
	[SerializeField, HideInInspector]
	private Collider[] existingColliders;
	private MeshFilter meshFilter;

	public int GroupsCount => groups.Count;

	public void ApplyAndRemove()
	{
		DestroyImmediate(this);
	}

	private void OnEnable()
	{
		if (existingColliders != null)
			BeforeStopEditing();
		meshFilter = GetComponent<MeshFilter>();
		if (meshes.Any(m => m == null || !m))
			UpdateMeshesAndColliders();
	}

	public void BeforeStartEditing()
	{
		if (existingColliders != null)
			BeforeStopEditing();
		existingColliders = GetComponents<Collider>().Where(c => c.enabled).ToArray();
		foreach (var collider in existingColliders)
		{
			collider.enabled = false;
		}
	}

	public void BeforeStopEditing()
	{
		if (existingColliders == null) return;

		foreach (var collider in existingColliders)
			if (collider)
				collider.enabled = true;

		existingColliders = null;

		var meshColliders = GetComponents<MeshCollider>();
		if (meshColliders.Length < groups.Count)
		{
			for (int i = 0; i < groups.Count - meshColliders.Length; i++)
			{
				var col = gameObject.AddComponent<MeshCollider>();
				col.sharedMesh = null;
				col.convex = true;
			}

			meshColliders = GetComponents<MeshCollider>();
		}
		for (int i = 0; i < meshColliders.Length; i++)
		{
			if (i < groups.Count)
				meshColliders[i].sharedMesh = meshes[i];
			else
				DestroyImmediate(meshColliders[i]);
		}
	}

	public void AddTriangle(int i, int triangleIndex)
	{
		groups[i] = groups[i].AddTriangle(triangleIndex);
		UpdateMesh(i);
	}

	public void RemoveTriangle(int i, int triangleIndex)
	{
		groups[i] = groups[i].RemoveTriangle(triangleIndex);
		UpdateMesh(i);
	}

	public void UpdateMesh(int i)
	{
		meshes[i] = meshFilter.sharedMesh.GetMeshFromTriangles(groups[i].indexes, groups[i].name, groups[i].color);
	}

	public void FixMissingGroups()
	{
		if (groups.Count > 0) return;
		AddGroup();
	}

	public void AddGroup()
	{
		groups.Add(new TriangleGroup());
		meshes.Add(new Mesh());
	}
	public void RemoveGroup(int i)
	{
		groups.RemoveAt(i);
		meshes.RemoveAt(i);
	}

	public (string name, Color color) GetGroupInfo(int i) => (groups[i].name, groups[i].color);

	public void SetGroupNameColor(int i, (string name, Color color) info)
	{
		var g = groups[i];
		g.name = info.name;
		g.color = info.color;
		groups[i] = g;
		meshes[i].name = info.name;
	}

	public void FixGroupColors()
	{
		for (int i = 0; i < groups.Count; i++)
		{
			if (groups[i].color == new Color())
			{
				TriangleGroup temp = groups[i];
				temp.color = Color.HSVToRGB((i * .782f) % 1f, 1f, 1f);
				groups[i] = temp;
			}
		}
	}

	public Mesh GetMesh(int i) => meshes[i];

	public void UpdateMeshesAndColliders()
	{
		for (int i = 0; i < groups.Count; i++)
		{
			UpdateMesh(i);
		}
		BeforeStopEditing();
	}

	public void TryRestoring()
	{
		var origVertices = meshFilter.sharedMesh.vertices;
		var origNormals = meshFilter.sharedMesh.normals;
		var origTriangles = meshFilter.sharedMesh.triangles;
		var meshColliders = GetComponents<MeshCollider>();
		for (int i = 0; i < meshColliders.Length; i++)
		{
			var collider = meshColliders[i];

			var indexes = collider.sharedMesh.GetOriginalMeshTriangleIndexes(meshFilter.sharedMesh, out var name, out var color);
			groups.Add(new TriangleGroup() { indexes = indexes, name = name, color = color });
			meshes.Add(collider.sharedMesh);
		}
	}
}

[Serializable]
public struct TriangleGroup
{
	public int[] indexes;
	public string name;
	public Color color;
	public TriangleGroup AddTriangle(int i)
	{
		if (indexes == null) indexes = new int[0];
		var tempList = indexes.ToList();
		tempList.Add(i);
		indexes = tempList.Distinct().ToArray();
		return this;
	}
	public TriangleGroup RemoveTriangle(int i)
	{
		if (indexes == null) indexes = new int[0];
		var tempList = indexes.ToList();
		tempList.Remove(i);
		indexes = tempList.ToArray();
		return this;
	}
}