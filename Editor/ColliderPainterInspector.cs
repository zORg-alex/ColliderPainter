using System.Linq;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;

[CustomEditor(typeof(ColliderPainter))]
public class ColliderPainterInspector : ColliderPainterInspectorBase
{
	private int controlID;
	private Color stopEditingColor = new Color(1f, .5f, .5f);
	private Color startEditingColor = new Color(.2f, .9f, .2f);
	private Color applyButtonColor = new Color(.7f, .7f, 1f);
	private Material material;
	private int materialColor;
	private MaterialPropertyBlock materialPropertyBlock;
	private GUIStyle centeredLabelStyle = new GUIStyle();
	private GUIStyle boxStyle = new GUIStyle();

	private GUIContent SceneGUIPaintIcon;
	private GUIContent InspectorGUIPaintIcon;
	private bool _Ctrl;
	private bool debugFoldout;
	private GUIContent debugFoldoutContent = new GUIContent("Debug Tools");


	private readonly AnimBool showDebug = new AnimBool();
	private static bool showDebugFoldout;

	private readonly AnimBool showTool = new AnimBool();
	private static bool showToolFoldout = true;


	private string MainStartButtonTooltip => "This mode locks selection to start painting.";

	private string GetEditButtonText(bool isEditing) => isEditing ? "Stop Editing" : "Start Editing";

	protected override void OnEnable()
	{
		base.OnEnable();
		SceneGUIPaintIcon = EditorGUIUtility.IconContent("Grid.PaintTool");
		InspectorGUIPaintIcon = EditorGUIUtility.IconContent("Grid.PaintTool");
		InspectorGUIPaintIcon.tooltip = MainStartButtonTooltip;
		var s = Shader.Find("Hidden/Internal-Colored");
		material = new Material(s);
		materialColor = Shader.PropertyToID("_Color");
		materialPropertyBlock = new MaterialPropertyBlock();
		if (!EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector)) return;

		try
		{
			centeredLabelStyle = new GUIStyle(EditorStyles.label);
			centeredLabelStyle.alignment = TextAnchor.MiddleCenter;
			boxStyle = "HelpBox";
		}

		//Letting slip some calls after Assembly reload before things are ready
		catch { }

		showDebug.valueChanged.AddListener(Repaint);
		showDebug.value = showDebugFoldout;
		showTool.valueChanged.AddListener(SceneView.RepaintAll);
		showTool.value = showToolFoldout;
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		showDebug.valueChanged.RemoveListener(Repaint);
	}

	public override void DrawSceneGUI()
	{
		var current = Event.current;
		Rect windowPosition = new Rect(20, 20, 300, WindowHeight);
		if (isEditing || windowPosition.Contains(current.mousePosition))
		{
			controlID = GUIUtility.GetControlID(37794724, FocusType.Passive);
			if (Event.current.type == EventType.Layout)
				//Magic thing to stop mouse from selecting other objects
				HandleUtility.AddDefaultControl(controlID);
		}

		GUI.Box(windowPosition.Expand(4), GUIContent.none, boxStyle);
		GUILayout.BeginArea(windowPosition);
		DrawSceneWindow();
		GUILayout.EndArea();
	}
	
	private float WindowHeight => showTool.faded * ((EditorGUIUtility.singleLineHeight + 4f) * targetScript.GroupsCount) + EditorGUIUtility.singleLineHeight + 4f;
	private void DrawSceneWindow()
	{
		var toolsRect = GUILayoutUtility.GetRect(300, 20).Expand(-3f,0f);
		Rect addButtonRect = toolsRect.Right(20);

		showToolFoldout = showTool.target = EditorGUI.Foldout(toolsRect, showTool.target, "ColliderPainter", true);
		if (EditorGUILayout.BeginFadeGroup(showTool.faded))
		{
			if (GUI.Button(addButtonRect, "+"))
				targetScript.AddGroup();

			for (int i = 0; i < targetScript.GroupsCount; i++)
			{
				GUILayout.BeginHorizontal();
				var group = targetScript.GetGroupInfo(i);
				var name = GUILayout.TextField(group.name);

				var color = group.color;
				if (isEditing)
					color = EditorGUILayout.ColorField(group.color, GUILayout.Width(70));

				if (group.name != name || group.color != color)
				{
					Undo.RecordObject(target, target.ToString() + this);
					group.name = name;
					group.color = color;
					targetScript.SetGroupNameColor(i, group);
				}
				DrawStartStopEditButton(false, i, GUILayout.Width(60));

				if (GUILayout.Button("-", GUILayout.Width(20)))
				{
					Undo.RecordObject(target, target.ToString() + this);
					targetScript.RemoveGroup(i);
					if (currentlyEditedGroup == i)
						StopEditing();
				}

				GUILayout.EndHorizontal();
			}
		}
		EditorGUILayout.EndFadeGroup();
	}

	public override void DrawSceneView()
	{
		var current = Event.current;

		if (current.type == EventType.KeyDown && (current.keyCode == KeyCode.LeftControl || current.keyCode == KeyCode.RightControl))
			_Ctrl = true;
		if (current.type == EventType.KeyUp && (current.keyCode == KeyCode.LeftControl || current.keyCode == KeyCode.RightControl))
			_Ctrl = false;

		if (currentlyEditedGroup >= 0 && current.button == 0 && (current.type == EventType.MouseDown || current.type == EventType.MouseDrag))
		{
			Undo.RecordObject(target, target.ToString() + this);
			var ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
			if (!painterMeshCollider) { Debug.LogError("ColliderPainter missing internal painting MeshCollider"); return; }
			if (!painterMeshCollider.Raycast(ray, out var hit, 10000)) return;
			if (_Ctrl)
				targetScript.RemoveTriangle(currentlyEditedGroup, hit.triangleIndex);
			else
				targetScript.AddTriangle(currentlyEditedGroup, hit.triangleIndex);
		}
	}

	public override void OnGUI()
	{
		if (targetScript.ForceEdit)
			StartEditing(0);

		//var c = GUI.color;

		DrawStartStopEditButton(true);

		//GUILayout.Space(10);
		//GUI.color = applyButtonColor;
		//if (GUILayout.Button("Apply and Remove"))
		//{
		//	Undo.RecordObject(target, target.name + " Applying Painted Collider");
		//	targetScript.ApplyAndRemove();
		//}
		//GUI.color = c;

		//GUILayout.Space(10);
		showDebugFoldout = showDebug.target = EditorGUILayout.Foldout(showDebug.target, "Debug", true);
		if (EditorGUILayout.BeginFadeGroup(showDebug.faded))
		{
			EditorGUI.indentLevel++;
			DefaultInspector();
			if (GUILayout.Button("Regenerate Meshes"))
			{
				Undo.RecordObject(target, target.name + " Fixing Painted Collider");
				targetScript.UpdateMeshesAndColliders();
			}
			EditorGUI.indentLevel--;
		}
		EditorGUILayout.EndFadeGroup();


		targetScript.FixGroupColors();
	}

	private void DrawStartStopEditButton(bool drawInspectorIcon, int i = -1, params GUILayoutOption[] options)
	{
		var c = GUI.color;
		bool isEditingThis = isEditing && (i < 0 || currentlyEditedGroup == i);
		GUI.color = isEditingThis ? stopEditingColor : startEditingColor;
		if (GUILayout.Button(drawInspectorIcon ? InspectorGUIPaintIcon : SceneGUIPaintIcon, options))
		{
			if (!isEditingThis)
				StartEditing(i);
			else
				StopEditing();
			SceneView.RepaintAll();
		}
		GUI.color = c;
	}

	public override void DrawGraphics()
	{
		var c = Gizmos.color;
		var matrix = targetScript.transform.localToWorldMatrix;
		for (int i = 0; i < targetScript.GroupsCount; i++)
		{
			var color = new Color(1,1,1,.5f);
			if (ColliderPainterInspectorBase.currentlyEditedGroup == i)
				color = Color.white;


			materialPropertyBlock.SetColor(materialColor, color);

			Mesh mesh = targetScript.GetMesh(i);
			if (mesh && mesh.vertices.Length > 0)
				Graphics.DrawMesh(targetScript.GetMesh(i), matrix, material, 0, null, 0, materialPropertyBlock);
		}
		Gizmos.color = c;
		Gizmos.matrix = Matrix4x4.identity;
	}

	[MenuItem("Tools/Start Collider Painter #p")]
	public static void MenuStartEditing()
	{
		var editedObject = Selection.activeGameObject;
		var script = editedObject.GetComponent<ColliderPainter>();
		if (script == null)
		{
			script = Selection.activeGameObject.AddComponent<ColliderPainter>();
			Component[] components = script.GetComponents<Component>();
			var ind = components.IndexOf(c => c.GetType() == typeof(ColliderPainter));
			if (ind == components.Length - 1)
				for (int i = 0; i < ind - 1; i++)
					UnityEditorInternal.ComponentUtility.MoveComponentUp(script);
			script.TryRestoring();
		}
		script.ForceEdit = true;
	}

	[MenuItem("Tools/Start Collider Painter #p", true)]
	public static bool CanMenuStartEditing() =>
		Selection.gameObjects.Length == 1 && Selection.activeGameObject.GetComponent<MeshFilter>() != null;
}
