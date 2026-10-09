using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace OpenRPGFirePitRaSampling
{
    [Serializable] public class Request { public string asset_path; public string compute_path; public string output_root; public string expected_guid; public string expected_file_id; }
    [Serializable] public class Mip { public int level; public string rgba_f32; public int grid = 64; }
    [Serializable] public class Report
    {
        public string unity_version, color_space, graphics_device_type, graphics_device_name;
        public string unity_guid, unity_file_id, graphics_format, filter_mode, wrap_u, wrap_v, wrap_w;
        public int width, height, mip_count, anisotropy;
        public float mip_bias;
        public List<Mip> mips = new List<Mip>();
        public bool source_shader_qualified = false, full_import = false;
    }
    public static class Export
    {
        public static void Run()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(args, "--openrpg-manifest");
                if (index < 0) throw new Exception("Missing request");
                Request request = JsonUtility.FromJson<Request>(File.ReadAllText(args[index + 1]));
                if (Directory.Exists(request.output_root)) throw new Exception("Preserve old output");
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(request.asset_path);
                ComputeShader compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(request.compute_path);
                if (texture == null || compute == null) throw new Exception("Missing native texture/compute");
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(texture, out string guid, out long fileId) ||
                    guid != request.expected_guid || fileId.ToString() != request.expected_file_id)
                    throw new Exception("Wrong source texture identity");
                if (texture.mipmapCount != 11 || texture.graphicsFormat.ToString() != "RGBA_DXT5_SRGB")
                    throw new Exception("Native storage format differs");
                Report report = new Report {
                    unity_version = Application.unityVersion, color_space = QualitySettings.activeColorSpace.ToString(),
                    graphics_device_type = SystemInfo.graphicsDeviceType.ToString(), graphics_device_name = SystemInfo.graphicsDeviceName,
                    unity_guid = guid, unity_file_id = fileId.ToString(), graphics_format = texture.graphicsFormat.ToString(),
                    width = texture.width, height = texture.height, mip_count = texture.mipmapCount,
                    filter_mode = texture.filterMode.ToString(), wrap_u = texture.wrapModeU.ToString(),
                    wrap_v = texture.wrapModeV.ToString(), wrap_w = texture.wrapModeW.ToString(),
                    anisotropy = texture.anisoLevel, mip_bias = texture.mipMapBias
                };
                Directory.CreateDirectory(request.output_root);
                int kernel = compute.FindKernel("SampleSource");
                using (ComputeBuffer buffer = new ComputeBuffer(4096, 16))
                {
                    compute.SetTexture(kernel, "SourceTexture", texture);
                    compute.SetBuffer(kernel, "Samples", buffer);
                    for (int level = 0; level < texture.mipmapCount; level++)
                    {
                        compute.SetFloat("MipLevel", level);
                        compute.Dispatch(kernel, 8, 8, 1);
                        Vector4[] values = new Vector4[4096];
                        buffer.GetData(values);
                        string path = Path.Combine(request.output_root, "mip-" + level + "-rgba.f32");
                        using (BinaryWriter writer = new BinaryWriter(new FileStream(path, FileMode.CreateNew)))
                            foreach (Vector4 value in values)
                                for (int channel = 0; channel < 4; channel++)
                                {
                                    float sample = value[channel];
                                    if (float.IsNaN(sample) || float.IsInfinity(sample) || sample < -0.000001f || sample > 1.000001f)
                                        throw new Exception("Invalid GPU sample");
                                    writer.Write(sample);
                                }
                        report.mips.Add(new Mip { level = level, rgba_f32 = path });
                    }
                }
                using (StreamWriter writer = new StreamWriter(new FileStream(Path.Combine(request.output_root, "RESULT.json"), FileMode.CreateNew)))
                    writer.Write(JsonUtility.ToJson(report, true));
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(3);
            }
        }
    }
}
