using Dullahan.Net;
using UnityEngine;
using UnityEditor;

namespace Dullahan.Editor
{
	[CustomEditor(typeof(Server))]
	public class ServerInspector : UnityEditor.Editor //TODO override inspector name for server?
	{
		public override void OnInspectorGUI()
		{
			GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
			DrawDefaultInspector ();
			GUI.enabled = true;
		}
	}
}