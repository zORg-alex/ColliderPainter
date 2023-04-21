using System;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[ExecuteAlways]
public class ColliderPainter : MonoBehaviour
{
	[SerializeField]
	internal TriangleGroup[] groups = new TriangleGroup[1];
	[SerializeField]
	internal Mesh[] meshes = new Mesh[1];
	[SerializeField]
	internal Mesh sharedMesh;

	[NonSerialized]
	public bool ForceEdit;
	[SerializeField, HideInInspector]
	internal Collider[] existingColliders;

	[SerializeField]
	internal UnityEngine.Object asset;	

	public int GroupsCount => groups.Length;

	public void ApplyAndRemove()
	{
		DestroyImmediate(this);
	}

	private void OnEnable()
	{
		if (existingColliders != null)
			RefreshMeshColliders();
		sharedMesh = GetComponent<MeshFilter>().sharedMesh;
		if (meshes.Any(m => m == null || !m))
			UpdateMeshesAndColliders();
		else
		{
			MeshCollider[] colliders = GetComponents<MeshCollider>();
			if (colliders.Length == meshes.Length && colliders.Any(c => !c.sharedMesh))
			{
				for (int i = 0; i < colliders.Length; i++)
				{
					colliders[i].sharedMesh = meshes[i];
				}
			}
			else
			{
				RefreshMeshColliders(colliders);
			}
		}
	}

	public void BeforeStartEditing()
	{
		if (existingColliders != null)
			RefreshMeshColliders();
		existingColliders = GetComponents<Collider>().Where(c => c.enabled).ToArray();
		foreach (var collider in existingColliders)
		{
			collider.enabled = false;
		}
	}

	public void RefreshMeshColliders(MeshCollider[] meshColliders = null)
	{
		if (existingColliders != null)
		{
			foreach (var collider in existingColliders)
				if (collider)
					collider.enabled = true;

			existingColliders = null;
		}

		if (meshColliders == null)
			meshColliders = GetComponents<MeshCollider>();
		if (meshColliders.Length < groups.Length)
		{
			for (int i = 0; i < groups.Length - meshColliders.Length; i++)
			{
				var col = gameObject.AddComponent<MeshCollider>();
				col.sharedMesh = null;
				col.convex = true;
			}

			meshColliders = GetComponents<MeshCollider>();
		}
		for (int i = 0; i < meshColliders.Length; i++)
		{
			if (i < groups.Length)
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
		if (groups[i].indexes.Length > 0)
			meshes[i] = sharedMesh.GetMeshFromTriangles(groups[i].indexes, groups[i].name, groups[i].color);
		else
			meshes[i] = null;
	}

	public void FixMissingGroups()
	{
		if (groups.Length > 0) return;
		AddGroup();
	}

	public void AddGroup()
	{
		groups = groups.Add(new TriangleGroup());
		meshes = meshes.Add(new Mesh());
	}
	public void RemoveGroup(int i)
	{
		groups = groups.RemoveAt(i);
		meshes = meshes.RemoveAt(i);
	}

	public (string name, Color color) GetGroupInfo(int i) => (groups[i].name, groups[i].color);

	public void SetGroupNameColor(int i, (string name, Color color) info)
	{
		var g = groups[i];
		g.name = info.name;
		g.color = info.color;
		groups[i] = g;
		if (meshes[i])
			meshes[i].name = info.name;
	}

	public void FixGroupColors()
	{
		for (int i = 0; i < groups.Length; i++)
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
		for (int i = 0; i < groups.Length; i++)
		{
			UpdateMesh(i);
		}
		RefreshMeshColliders();
	}

	public void TryRestoring()
	{
		var origVertices = sharedMesh.vertices;
		var origNormals = sharedMesh.normals;
		var origTriangles = sharedMesh.triangles;
		var meshColliders = GetComponents<MeshCollider>();
		for (int i = 0; i < meshColliders.Length; i++)
		{
			var collider = meshColliders[i];

			var indexes = collider.sharedMesh.GetOriginalMeshTriangleIndexes(sharedMesh, out var name, out var color);
			groups = groups.Add(new TriangleGroup() { indexes = indexes, name = name, color = color });
			meshes = meshes.Add(collider.sharedMesh);
		}
	}
}

[Serializable]
[DebuggerDisplay("{name} {indexes.Length} {color}")]
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