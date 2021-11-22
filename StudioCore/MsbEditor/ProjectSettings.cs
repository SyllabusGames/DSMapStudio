using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Numerics;

namespace StudioCore.MsbEditor
{
    /// <summary>
    /// Settings for a modding project. Gets serialized to JSON
    /// </summary>
    public class ProjectSettings
    {
        public string ProjectName { get; set; } = "";
        public string GameRoot { get; set; } = "";
        public GameType GameType { get; set; } = GameType.Undefined;

        public float CameraPosX { get; set; } = 0;
        public float CameraPosY { get; set; } = 0;
        public float CameraPosZ { get; set; } = 0;

        public float CameraRotX { get; set; } = 0;
        public float CameraRotY { get; set; } = 0;
        public float CameraRotZ { get; set; } = 0;
        public float CameraRotW { get; set; } = 1;

        public float CameraOrbitCenterX { get; set; } = 0;
        public float CameraOrbitCenterY { get; set; } = 0;
        public float CameraOrbitCenterZ { get; set; } = 0;

        //		These fields are updated by WorldView.SetCameraLocation()
        public float OrbitCamDistance { get; set; } = 2;
        public Vector3 CameraPosition = new Vector3(0,0,0);//		Vector3s don't serialize properly
        public Quaternion CameraRotation = new Quaternion(0,0,0,1);
        public Vector3 CameraOrbitCenter = new Vector3(0,0,0);


        /// <summary>
        /// Has different meanings depending on the game, but for supported games
        /// (DS2 and DS3) this means that params are written as "loose" i.e. outside
        /// the regulation file.
        /// </summary>
        public bool UseLooseParams { get; set; } = false;

        public void Serialize(string path)
        {
            CameraPosX = CameraPosition.X;
            CameraPosY = CameraPosition.Y;
            CameraPosZ = CameraPosition.Z;

            CameraRotX = CameraRotation.X;
            CameraRotY = CameraRotation.Y;
            CameraRotZ = CameraRotation.Z;
            CameraRotW = CameraRotation.W;

            CameraOrbitCenterX = CameraOrbitCenter.X;
            CameraOrbitCenterY = CameraOrbitCenter.Y;
            CameraOrbitCenterZ = CameraOrbitCenter.Z;

            var jsonString = JsonSerializer.SerializeToUtf8Bytes(this);//		Serialize all public properties (not fields) in this class.
            File.WriteAllBytes(path, jsonString);//		Save to file
        }

        public static ProjectSettings Deserialize(string path)
        {
            var jsonString = File.ReadAllBytes(path);
            var readOnlySpan = new ReadOnlySpan<byte>(jsonString);
            return JsonSerializer.Deserialize<ProjectSettings>(readOnlySpan);
        }
    }
}
