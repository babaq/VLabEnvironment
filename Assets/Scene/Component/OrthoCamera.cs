﻿/*
OrthoCamera.cs is part of the Experica.
Copyright (c) 2016 Li Alex Zhang and Contributors

Permission is hereby granted, free of charge, to any person obtaining a 
copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the 
Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included 
in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF 
OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
using UnityEngine;
using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Networking;
using System.Collections.Generic;
#if COMMAND
using Experica.Command;
#endif
#if ENVIRONMENT
using Experica.Environment;
#endif

namespace Experica
{
    [NetworkSettings(channel = 0, sendInterval = 0)]
    public class OrthoCamera : NetworkBehaviour
    {
        /// <summary>
        /// Distance from screen to eye in arbitory unit
        /// </summary>
        [SyncVar(hook = "onscreentoeye")]
        public float ScreenToEye = 57;
        /// <summary>
        /// Height of the viewport(i.e. height of the display if full screen), same unit as `ScreenToEye`
        /// </summary>
        [SyncVar(hook = "onscreenheight")]
        public float ScreenHeight = 30;
        /// <summary>
        /// Aspect ratio(width/height) of the viewport
        /// </summary>
        [SyncVar(hook = "onscreenaspect")]
        public float ScreenAspect = 4 / 3;
        /// <summary>
        /// Background color of the camera
        /// </summary>
        [SyncVar(hook = "onbgcolor")]
        public Color BGColor = Color.gray;
        [SyncVar(hook = "onmapcolor")]
        public bool MapColor = true;

        /// <summary>
        /// Height of the viewport in visual field degree
        /// </summary>
        public float Height
        {
            get { return camera.orthographicSize * 2; }
        }

        /// <summary>
        /// Width of the viewport in visual field degree
        /// </summary>
        public float Width
        {
            get { return camera.orthographicSize * 2 * camera.aspect; }
        }

        public float NearPlane
        {
            get { return transform.localPosition.z + camera.nearClipPlane; }
        }

        public float FarPlane
        {
            get { return transform.localPosition.z + camera.farClipPlane; }
        }

        public Action OnCameraChange;
        Camera camera;
        HDAdditionalCameraData camerahddata;
        Volume postprocessvolume;
        NetManager netmanager;

        void Awake()
        {
            camera = gameObject.GetComponent<Camera>();
            camerahddata = gameObject.GetComponent<HDAdditionalCameraData>();
            netmanager = FindObjectOfType<NetManager>();
#if COMMAND
            OnCameraChange += netmanager.uicontroller.viewpanel.UpdateViewport;
#endif
            tag = "MainCamera";
            transform.localPosition = new Vector3(0, 0, -1001);
            camera.nearClipPlane = 1;
            camera.farClipPlane = 2001;
            postprocessvolume = gameObject.GetComponent<Volume>();
        }

        void onscreentoeye(float d)
        {
            camera.orthographicSize = Mathf.Rad2Deg * Mathf.Atan2(ScreenHeight / 2, d);
            ScreenToEye = d;
            OnCameraChange?.Invoke();
        }

        void onscreenheight(float h)
        {
            camera.orthographicSize = Mathf.Rad2Deg * Mathf.Atan2(h / 2, ScreenToEye);
            ScreenHeight = h;
            OnCameraChange?.Invoke();
        }

        void onscreenaspect(float r)
        {
            camera.aspect = r;
            ScreenAspect = r;
            OnCameraChange?.Invoke();
        }

        void onbgcolor(Color c)
        {
            camerahddata.backgroundColorHDR = c;
            BGColor = c;
        }

        void onmapcolor(bool ismapcolor)
        {
            Tonemapping tonemapping;
            if (postprocessvolume.profile.TryGet(out tonemapping))
            {
                tonemapping.active = ismapcolor;
#if COMMAND
                if (ismapcolor)
                {
                    var cdclut = netmanager.uicontroller.CurrentDisplayCLUT;
                    if (cdclut != null)
                    {
                        tonemapping.lutTexture.value = cdclut;
                        RpcCLUT(cdclut.GetPixelData<byte>(0).ToArray(), cdclut.width);
                    }
                }
#endif
            }
            MapColor = ismapcolor;
        }

        [ClientRpc]
        void RpcCLUT(byte[] clut, int size)
        {
#if ENVIRONMENT
            Tonemapping tonemapping;
            if (postprocessvolume.profile.TryGet(out tonemapping))
            {
                var tex = new Texture3D(size, size, size, TextureFormat.RGB24, false);
                tex.SetPixelData(clut, 0);
                tex.Apply();
                tonemapping.lutTexture.value = tex;
            }
#endif
        }

#if COMMAND
        public override bool OnCheckObserver(NetworkConnection conn)
        {
            return netmanager.IsConnectionPeerType(conn, PeerType.Environment);
        }

        public override bool OnRebuildObservers(HashSet<NetworkConnection> observers, bool initialize)
        {
            var vcs = netmanager.GetPeerTypeConnection(PeerType.Environment);
            if (vcs.Count > 0)
            {
                foreach (var c in vcs)
                {
                    observers.Add(c);
                }
                return true;
            }
            return false;
        }
#endif

    }
}