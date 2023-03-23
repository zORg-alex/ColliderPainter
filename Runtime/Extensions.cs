using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class Extensions
{
	public static int IndexOf<T>(this T[] array, Func<T, bool> selector)
	{
		for (int i = 0; i < array.Length; i++)
			if (selector(array[i])) return i;
		return -1;
	}
	public static int IndexOf<T>(this T[] array, T value) => Array.IndexOf(array, value);
	public static void Reset(this Transform transform)
	{
		transform.localPosition = Vector3.zero;
		transform.localRotation = Quaternion.identity;
		transform.localScale = Vector3.one;
	}
	public static Mesh BareCopy(this Mesh mesh)
	{
		var m = new Mesh();
		m.vertices = mesh.vertices;
		m.normals = mesh.normals;
		return m;
	}
	public static Color MultiplyAlpha(this Color color, float alpha)
	{
		color.a *= alpha;
		return color;
	}

	public static int[] GetOriginalMeshTriangleIndexes(this Mesh mesh, Mesh original, out string name, out Color color)
	{
		var indexes = new int[mesh.triangles.Length / 3];

		var originalVertices = original.vertices;
		var origNormals = original.normals;
		var origTriangles = original.triangles;
		var triangles = mesh.triangles;
		var vertices = mesh.vertices;
		var normals = mesh.normals;
		var origVertIndexes = new int[vertices.Length];
		var prevVind = 0;
		for (int j = 0; j < vertices.Length; j++)
		{
			for (int k = 0; k < originalVertices.Length - prevVind; k++)
			{
				if (originalVertices[k + prevVind] == vertices[j] && origNormals[k + prevVind] == normals[j])
				{
					origVertIndexes[j] = k + prevVind;
					prevVind += k;
					break;
				}
			}
			if (origVertIndexes[j] != prevVind)
				break;
		}
		var trianglesInOriginalVertices = triangles.SelectToArray(v => origVertIndexes[v]);
		for (int j = 0; j < triangles.Length; j += 3)
		{
			var remappedColliderTriangles = triangles.SelectToArray(v => origVertIndexes[v]);
			for (int k = 0; k < origTriangles.Length; k += 3)
			{
				if (origTriangles[k] == remappedColliderTriangles[j] &&
					origTriangles[k + 1] == remappedColliderTriangles[j + 1])
				{
					indexes[j / 3] = k / 3;
					break;
				}
			}
		}

		name = mesh.name;
		color = mesh.colors.Length > 0 ? mesh.colors[0] : default;

		return indexes;
	}

	public static Mesh GetMeshFromTriangles(this Mesh mesh, int[] indexes, string name, Color color = default)
	{
		var m = new Mesh();
		var tris = new int[indexes.Length * 3];
		for (int i = 0; i < indexes.Length; i++)
		{
			int triInd = indexes[i] * 3;
			tris[i * 3] = mesh.triangles[triInd];
			tris[i * 3 + 1] = mesh.triangles[triInd + 1];
			tris[i * 3 + 2] = mesh.triangles[triInd + 2];
		}
		var uniqueOldVerticeIndexes = tris.Distinct().ToArray();
		Array.Sort(uniqueOldVerticeIndexes);
		m.vertices = uniqueOldVerticeIndexes.Select(i => mesh.vertices[i]).ToArray();
		m.normals = uniqueOldVerticeIndexes.Select(i => mesh.normals[i]).ToArray();
		m.triangles = tris.Select((vi, i) => uniqueOldVerticeIndexes.IndexOf(vi)).ToArray();
		Color[] colors = new Color[uniqueOldVerticeIndexes.Length];
		for (int i = 0; i < colors.Length; i++)
			colors[i] = color;
		m.colors = colors;
		m.name = name;
		//m.Optimize();
		return m;
	}

	public static Rect Expand(this Rect rect, float expand)
	{
		rect.x -= expand;
		rect.y -= expand;
		rect.height += expand * 2f;
		rect.width += expand * 2f;
		return rect;
	}

	public static Rect Expand(this Rect rect, float width, float height)
	{
		rect.x -= width;
		rect.y -= height;
		rect.height += height * 2f;
		rect.width += width * 2f;
		return rect;
	}

	public static Rect Right(this ref Rect rect, float width, float space = 2f)
	{
		float num = Math.Min(rect.width, width + space);
		Rect result = rect.AlignRight(num - space);
		rect.width -= num;
		return result;
	}
	public static Rect AlignRight(this Rect rect, float width)
	{
		rect.x = rect.x + rect.width - width;
		rect.width = width;
		return rect;
	}

	public static T2[] SelectToArray<T1, T2>(this T1[] array, Func<T1, T2> selector)
	{
		var r = new T2[array.Length];
		for (var i = 0; i < array.Length; i++)
		{
			r[i] = selector(array[i]);
		}
		return r;
	}

	public static T[] Add<T>(this T[] array, T item)
	{
		var r = new T[array.Length + 1];
		for (int i = 0; i < array.Length; i++)
		{
			r[i] = array[i];
		}
		r[array.Length] = item;
		return r;
	}
	public static T[] Remove<T>(this T[] array, T item)
	{
		var r = new T[array.Length - 1];
		bool itemFound = false;
		for (int i = 0, j = 0; i < array.Length; j++, i++)
		{
			if (item.Equals(array[j]))
			{ i++; itemFound = true; }
			r[i] = array[j];
		}
		return itemFound ? r : array;
	}
	public static T[] RemoveAt<T>(this T[] array, int ind)
	{
		if (ind < 0 || ind > array.Length)
			return array;
		var r = new T[array.Length - 1];
		for (int i = 0, j = 0; i < r.Length; j++, i++)
		{
			if (j == ind)
				j++;
			r[i] = array[j];
		}
		return r;
	}

	public static void PositionAsChildOfNonRigidBody(this Transform transform, Transform parentAsTo, out Transform[] temp)
	{
		if (parentAsTo.GetComponentInParent<Rigidbody>() is Rigidbody parentRigidBody)
		{
			var list = new List<Transform>();
			Reconstruct(parentAsTo, parentRigidBody.transform, ref list, transform);
			temp = list.ToArray();
		}
		else
		{
			transform.parent = parentAsTo;
			transform.Reset();
			temp = new Transform[] { transform };
		}

		Transform Reconstruct(Transform transformCursor, Transform finalTransform, ref List<Transform> l, Transform copiedTransform = null)
		{
			Transform t;
			if (copiedTransform == null)
				copiedTransform = new GameObject().transform;
			if (transformCursor != finalTransform)
			{
				copiedTransform.parent = Reconstruct(transformCursor.parent, finalTransform, ref l);
			}
			else
			{
				copiedTransform.parent = finalTransform.parent;
			}
#if UNITY_2019
			copiedTransform.localPosition = transformCursor.localPosition;
			copiedTransform.localRotation = transformCursor.localRotation;
#else
			copiedTransform.SetLocalPositionAndRotation(transformCursor.localPosition, transformCursor.localRotation);
#endif
			copiedTransform.localScale = transformCursor.localScale;
			l.Add(copiedTransform);
			return copiedTransform;
		}
	}

	public static void DestroyImmediate(this IEnumerable<Transform> transforms)
	{
		foreach (Transform t in transforms)
			UnityEngine.Object.DestroyImmediate(t.gameObject);
	}
}
