using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace LineworkLite.Editor.FreeOutline
{
    public static class SmoothNormalsBaker
    {
        public static Vector2[] ComputeSmoothedNormals(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var tangents = mesh.tangents;

            var vertexCount = vertices.Length;

            if (tangents.Length == 0)
            {
                Debug.LogError($"Mesh {mesh.name} did not contain any tangents.");
                return null;
            }

            const Allocator allocator = Allocator.TempJob;
#if HAS_PACKAGE_UNITY_COLLECTIONS_2_1_0_EXP_4
            var smoothedNormalsMap = new UnsafeParallelHashMap<Vector3, Vector3>(vertexCount, allocator);
#else
            var smoothedNormalsMap = new UnsafeHashMap<Vector3, Vector3>(vertexCount, allocator);
#endif
            var normalsNativeArray = default(NativeArray<Vector3>);
            var verticesNativeArray = default(NativeArray<Vector3>);
            var tangentsNativeArray = default(NativeArray<Vector4>);
            var bakedNormals = default(NativeArray<Vector2>);

            try
            {
                for (var i = 0; i < vertexCount; i++)
                {
                    if (smoothedNormalsMap.ContainsKey(vertices[i]))
                    {
                        smoothedNormalsMap[vertices[i]] += normals[i];
                    }
                    else
                    {
                        smoothedNormalsMap.Add(vertices[i], normals[i]);
                    }
                }

                normalsNativeArray = new NativeArray<Vector3>(normals, allocator);
                verticesNativeArray = new NativeArray<Vector3>(vertices, allocator);
                tangentsNativeArray = new NativeArray<Vector4>(tangents, allocator);
                bakedNormals = new NativeArray<Vector2>(vertexCount, allocator);

                var bakeNormalJob = new BakeNormalJob(verticesNativeArray, normalsNativeArray, tangentsNativeArray, smoothedNormalsMap, bakedNormals);
                bakeNormalJob.Schedule(vertexCount, 100).Complete();

                var bakedSmoothedNormals = new Vector2[vertexCount];
                bakedNormals.CopyTo(bakedSmoothedNormals);
                return bakedSmoothedNormals;
            }
            finally
            {
                if (smoothedNormalsMap.IsCreated)
                    smoothedNormalsMap.Dispose();

                if (normalsNativeArray.IsCreated)
                    normalsNativeArray.Dispose();

                if (verticesNativeArray.IsCreated)
                    verticesNativeArray.Dispose();

                if (tangentsNativeArray.IsCreated)
                    tangentsNativeArray.Dispose();

                if (bakedNormals.IsCreated)
                    bakedNormals.Dispose();
            }
        }

        private struct BakeNormalJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Vector3> vertices, normals;
            [ReadOnly] public NativeArray<Vector4> tangents;
            [NativeDisableContainerSafetyRestriction]
#if HAS_PACKAGE_UNITY_COLLECTIONS_2_1_0_EXP_4
            [ReadOnly] public UnsafeParallelHashMap<Vector3, Vector3> smoothedNormals;
#else
              [ReadOnly] public UnsafeHashMap<Vector3, Vector3> smoothedNormals;
#endif
            [WriteOnly] public NativeArray<Vector2> bakedNormals;

            public BakeNormalJob(
                NativeArray<Vector3> vertices,
                NativeArray<Vector3> normals,
                NativeArray<Vector4> tangents,
#if HAS_PACKAGE_UNITY_COLLECTIONS_2_1_0_EXP_4
                UnsafeParallelHashMap<Vector3, Vector3> smoothedNormals,
#else
                  UnsafeHashMap<Vector3, Vector3> smoothedNormals,
#endif
                NativeArray<Vector2> bakedNormals)
            {
                this.vertices = vertices;
                this.normals = normals;
                this.tangents = tangents;
                this.smoothedNormals = smoothedNormals;
                this.bakedNormals = bakedNormals;
            }

            void IJobParallelFor.Execute(int index)
            {
                var smoothedNormal = smoothedNormals[vertices[index]];

                var normalOS = normals[index].normalized;
                Vector3 tangentOS = tangents[index];
                tangentOS = tangentOS.normalized;
                var bitangentOS = (Vector3.Cross(normalOS, tangentOS) * tangents[index].w).normalized;

                var tbn = new Matrix4x4(tangentOS, bitangentOS, normalOS, Vector3.zero);
                tbn = tbn.transpose;

                var bakedNormal = OctahedronNormal(tbn.MultiplyVector(smoothedNormal).normalized);

                bakedNormals[index] = bakedNormal;
            }

            private static Vector2 OctahedronNormal(Vector3 resultNormal)
            {
                var absVec = new Vector3(Mathf.Abs(resultNormal.x), Mathf.Abs(resultNormal.y), Mathf.Abs(resultNormal.z));
                var octNormal = (Vector2) resultNormal / Vector3.Dot(Vector3.one, absVec);
                if (!(resultNormal.z <= 0)) return octNormal;
                var absY = Mathf.Abs(octNormal.y);
                var value = (1 - absY) * (octNormal.y >= 0 ? 1 : -1);
                octNormal = new Vector2(value, value);
                return octNormal;
            }
        }
    }
}
