using UnityEngine;

public class RuntimeStereoSpawnTest : MonoBehaviour
{
	[SerializeField] private Material runtimeDebugMaterial;

	private void Start()
	{
		// Temporary XR stereo diagnostic: compare runtime unparented vs parented primitives.
		// Temporary runtime material diagnostic: optionally force one shared material on all runtime primitives.
		GameObject runtimeUnparentedCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
		runtimeUnparentedCube.name = "RuntimeUnparentedCube";
		runtimeUnparentedCube.transform.position = new Vector3(0f, 1.5f, 4f);
		ApplyRuntimeDebugMaterial(runtimeUnparentedCube);

		GameObject runtimeUnparentedSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		runtimeUnparentedSphere.name = "RuntimeUnparentedSphere";
		runtimeUnparentedSphere.transform.position = new Vector3(1f, 1.5f, 4f);
		ApplyRuntimeDebugMaterial(runtimeUnparentedSphere);

		GameObject runtimeTestParent = new GameObject("RuntimeTestParent");
		runtimeTestParent.transform.position = Vector3.zero;
		runtimeTestParent.transform.rotation = Quaternion.identity;
		runtimeTestParent.transform.localScale = Vector3.one;

		GameObject runtimeParentedCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
		runtimeParentedCube.name = "RuntimeParentedCube";
		runtimeParentedCube.transform.position = new Vector3(0f, 1.5f, 5f);
		ApplyRuntimeDebugMaterial(runtimeParentedCube);
		runtimeParentedCube.transform.SetParent(runtimeTestParent.transform, true);

		GameObject runtimeParentedSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		runtimeParentedSphere.name = "RuntimeParentedSphere";
		runtimeParentedSphere.transform.position = new Vector3(1f, 1.5f, 5f);
		ApplyRuntimeDebugMaterial(runtimeParentedSphere);
		runtimeParentedSphere.transform.SetParent(runtimeTestParent.transform, true);
	}

	private void ApplyRuntimeDebugMaterial(GameObject runtimePrimitive)
	{
		if (runtimeDebugMaterial == null)
		{
			return;
		}

		Renderer renderer = runtimePrimitive.GetComponent<Renderer>();
		if (renderer != null)
		{
			renderer.sharedMaterial = runtimeDebugMaterial;
		}
	}
}