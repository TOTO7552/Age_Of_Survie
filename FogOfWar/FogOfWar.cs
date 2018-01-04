using UnityEngine;
using System.Collections.Generic;

class ColliderFogRect
{
    public Vector2i position;
    public Vector2i size;
    public int xMin { get { return position.x; } set { size.x -= value - position.x; position.x = value; } }
    public int yMin { get { return position.y; } set { size.y -= value - position.y; position.y = value; } }
    public int xMax { get { return position.x + size.x; } set { size.x = value - position.x; } }
    public int yMax { get { return position.y + size.y; } set { size.y = value - position.y; } }

    public ColliderFogRect(Collider c, FogOfWar fow)
    {
        Bounds b = c.bounds;
        position = fow.WorldPositionToFogPosition(b.min);
        size = fow.WorldPositionToFogPosition(b.max) - position;
    }

    public ColliderFogRect(Collider2D c, FogOfWar fow)
    {
        Bounds b = c.bounds;
        position = fow.WorldPositionToFogPosition(b.min);
        size = fow.WorldPositionToFogPosition(b.max) - position;
    }

    public bool Contains(Vector2i p)
    {
        return p.x >= xMin && p.x <= xMax && p.y >= yMin && p.y <= yMax;
    }

    public bool Contains(ColliderFogRect other)
    {
        return other.xMin >= xMin && other.xMax <= xMax &&
            other.yMin >= yMin && other.yMax <= yMax;
    }

    public bool ContainsCircle(Vector2i p, int r)
    {
        return p.x - r >= xMin && p.x + r <= xMax &&
            p.y - r >= yMin && p.y + r <= yMax;
    }

    public void ExtendToCircleEdge(Vector2i p, int radius)
    {
        if (xMin < p.x)
            xMin = p.x - radius;
        if (xMax > p.x)
            xMax = p.x + radius;
        if (yMin < p.y)
            yMin = p.y - radius;
        if (yMax > p.y)
            yMax = p.y + radius;
    }
}

class ColliderFogRectList : List<ColliderFogRect>
{
    public FogOfWar fogOfWar { get; private set; }

    public ColliderFogRectList(FogOfWar fow)
    {
        fogOfWar = fow;
    }

    public void Add(params Collider[] colliders)
    {
        for (int i = 0; i < colliders.Length; ++i)
            Add(new ColliderFogRect(colliders[i], fogOfWar));
    }

    public void Add(params Collider2D[] colliders)
    {
        for (int i = 0; i < colliders.Length; ++i)
            Add(new ColliderFogRect(colliders[i], fogOfWar));
    }

    public bool Contains(Vector2i p)
    {
        for (int i = 0; i < Count; ++i)
        {
            if (this[i].Contains(p))
                return true;
        }
        return false;
    }

    public void Optimise()
    {
        // remove any rects that are contained within eachother
        this.RemoveAll(r => { for (int i = 0; i < Count; ++i) { if (this[i] != r && this[i].Contains(r)) return true; } return false; });
    }

    public void ExtendToCircleEdge(Vector2i p, int radius)
    {
        for (int i = 0; i < Count; ++i)
            this[i].ExtendToCircleEdge(p, radius);
    }
}

class FogFill
{
    public FogOfWar fogOfWar { get; private set; }

    public Vector2i position { get; private set; }
    public Vector3 worldPosition { get; private set; }
    public Vector2 forward { get; private set; }
    public float dotAngle { get; private set; }

    public int xStart { get; private set; }
    public int xEnd { get; private set; }
    public int yStart { get; private set; }
    public int yEnd { get; private set; }

    public int radius { get; private set; }
    public float worldRadius { get; private set; }
    public int radiusSqr { get; private set; }
    public int innerRadius { get; private set; }
    public int innerRadiusSqr { get; private set; }

    public FogFill(FogOfWar fow, Vector3 worldpos, float worldradius, float viewangle, Vector3 worldforward)
    {
        fogOfWar = fow;
        worldPosition = worldpos;
        worldRadius = worldradius;

        position = fow.WorldPositionToFogPosition(worldpos);
        radius = (int)(worldRadius * (float)fow.mapResolution / fow.mapSize);
        dotAngle = 1.0f - viewangle / 90;
        forward = fow.WorldPositionToModePosition(worldforward);

        xStart = Mathf.Clamp(position.x - radius, 1, fow.mapResolution - 1);
        xEnd = Mathf.Clamp(xStart + radius + radius, 1, fow.mapResolution - 1);
        yStart = Mathf.Clamp(position.y - radius, 1, fow.mapResolution - 1);
        yEnd = Mathf.Clamp(yStart + radius + radius, 1, fow.mapResolution - 1);

        radiusSqr = radius * radius;
        innerRadius = (int)((1.0f - fow.fogEdgeRadius) * radius);
        innerRadiusSqr = innerRadius * innerRadius;
    }

    bool IsInViewCone(Vector2 dir)
    {
        if (dotAngle <= -0.99f)
            return true;
        return Vector2.Dot(dir, forward) > dotAngle;
    }

    public void UnfogCircle(byte[] values)
    {
        for (int y = yStart; y < yEnd; ++y)
        {
            for (int x = xStart; x < xEnd; ++x)
            {
                int index = y * fogOfWar.mapResolution + x;

                // do nothing if it is already completely unfogged
                if (values[index] == 0)
                    continue;

                Vector2i offset = new Vector2i(x - position.x, y - position.y);

                // do nothing if too far too see
                int sqrdistance = offset.sqrMagnitude;
                if (sqrdistance >= radiusSqr)
                    continue;

                // is this point in the view cone?
                Vector2 dir = offset.normalized;
                if (!IsInViewCone(dir))
                    continue;

                // fully unfogged
                if (sqrdistance <= innerRadiusSqr)
                    values[index] = 0;

                // partially fogged (lerp between innerradius and radius)
                else// if (sqrdistance <= radiusSqr)
                {
                    float t = (Mathf.Sqrt(sqrdistance) - innerRadius) / (radius - innerRadius);
                    byte v = (byte)Mathf.Lerp(0, 255, t);
                    if (v < values[index])
                        values[index] = v;
                }
            }
        }
    }

    bool Raycast(float sqrdistance, Vector2i offset, int layermask, out float distance)
    {
        distance = 0.0f;
        float hitdistance = Mathf.Sqrt(sqrdistance) * fogOfWar.pixelSize;

        if (fogOfWar.mode == FogOfWarMode.Mode2D)
        {
            RaycastHit2D hit = Physics2D.Raycast(worldPosition, offset.vector2, hitdistance, layermask);
            if (hit.collider == null)
                return false;
            distance = hit.distance;
        }
        else// if (fogOfWar.mode == FogOfWarMode.Mode3D)
        {
            RaycastHit hit;
            if (!Physics.Raycast(worldPosition, new Vector3(offset.x, 0, offset.y), out hit, hitdistance, layermask))
                return false;
            distance = hit.distance;
        }
        return true;
    }

    public void UnfogCircleLineOfSight(byte[] values, ColliderFogRectList excluderects, int layermask)
    {
        for (int y = yStart; y < yEnd; ++y)
        {
            for (int x = xStart; x < xEnd; ++x)
            {
                int index = y * fogOfWar.mapResolution + x;

                // do nothing if it is already completely unfogged
                if (values[index] == 0)
                    continue;

                Vector2i offset = new Vector2i(x - position.x, y - position.y);

                // do nothing if too far too see
                int sqrdistance = offset.sqrMagnitude;
                if (sqrdistance >= radiusSqr)
                    continue;

                // is this point in the view cone?
                Vector2 dir = offset.normalized;
                if (!IsInViewCone(dir))
                    continue;

                // if it could be hidden, raycast to make sure
                if (excluderects.Contains(new Vector2i(x, y)))
                {
                    // perform raycast
                    float hitdistance;
                    if (Raycast(sqrdistance, offset, layermask, out hitdistance))
                    {

                        // optimisation
                        if (fogOfWar.fieldOfViewPenetration == 0.0f)
                            continue;

                        // offset the pixel back so that we can see what we are looking at
                        // TODO: This could be optimised by keeping it as ints
                        float pixeldistsqr = new Vector2(offset.x * fogOfWar.pixelSize, offset.y * fogOfWar.pixelSize).sqrMagnitude;
                        float penetration = Mathf.Min(hitdistance + fogOfWar.fieldOfViewPenetration, worldRadius);
                        if (pixeldistsqr >= penetration * penetration)
                            continue;
                    }
                }

                // fully unfogged
                if (sqrdistance <= innerRadiusSqr)
                    values[index] = 0;

                // partially fogged (lerp between innerradius and radius)
                else// if (sqrdistance <= radiusSqr)
                {
                    float t = (Mathf.Sqrt(sqrdistance) - innerRadius) / (radius - innerRadius);
                    byte v = (byte)Mathf.Lerp(0, 255, t);
                    if (v < values[index])
                        values[index] = v;
                }
            }
        }
    }
}

public enum FogOfWarMode
{
    Mode2D,
    Mode3D
}

public class FogOfWar : MonoBehaviour
{
    public int mapResolution = 128;
    public float mapSize = 128;
    public Vector2 mapOffset = Vector2.zero;
    public FogOfWarMode mode = FogOfWarMode.Mode3D;
    public FilterMode filterMode = FilterMode.Bilinear;
    public Color fogColor = Color.black;
    public Texture2D fogColorTexture = null;
    public bool clearFog = false;
    public LayerMask clearFogMask = -1;
    public float pixelSize { get { return mapSize / mapResolution; } }
    public float fieldOfViewPenetration = 1.0f;

    [Range(0.0f, 1.0f)]
    public float fogEdgeRadius = 0.2f;

    [Range(0.0f, 1.0f)]
    public float partialFogAmount = 0.5f;

    Material _material;
    public Texture2D texture { get; private set; }
    byte[] _values;

    public float updateFrequency = 0.1f;
    float _nextUpdate = 0.0f;

    Transform _transform;
    Camera _camera;

    public static FogOfWar current = null;

    static Shader _fogOfWarShader = null;
    public static Shader fogOfWarShader { get { if (_fogOfWarShader == null) _fogOfWarShader = Resources.Load<Shader>("FogOfWarShader"); return _fogOfWarShader; } }
    static Shader _clearFogShader = null;
    public static Shader clearFogShader { get { if (_clearFogShader == null) _clearFogShader = Resources.Load<Shader>("ClearFogShader"); return _clearFogShader; } }

    void Awake()
    {
        current = this;

        texture = new Texture2D(mapResolution, mapResolution, TextureFormat.Alpha8, false);

        _material = new Material(fogOfWarShader);
        _material.name = "FogMaterial";
        _material.SetTexture("_FogTex", texture);
        _material.SetInt("_FogTextureSize", mapResolution);
        _material.SetFloat("_MapSize", mapSize);
        _material.SetVector("_MapOffset", mapOffset);

        _values = new byte[mapResolution * mapResolution];
        SetAll(255);
    }

    void Start()
    {
        _transform = transform;
        _camera = GetComponent<Camera>();
        _camera.depthTextureMode |= DepthTextureMode.Depth;

        UpdateTexture();
    }

    public void SetAll(byte value)
    {
        for (int i = 0; i < _values.Length; ++i)
            _values[i] = value;
    }

    public Vector2 WorldPositionToModePosition(Vector3 position)
    {
        if (mode == FogOfWarMode.Mode2D)
            return new Vector2(position.x, position.y);
        //else if (mode == FogOfWarMode.Mode3D)
            return new Vector2(position.x, position.z);
    }

    public Vector2i WorldPositionToFogPosition(Vector3 position)
    {
        float mapmultiplier = (float)mapResolution / mapSize;
        Vector2i mappos = new Vector2i((WorldPositionToModePosition(position) - mapOffset) * mapmultiplier);
        mappos += new Vector2i(mapResolution >> 1, mapResolution >> 1);
        return mappos;
    }

    public Vector2 FogPositionToWorldPosition(Vector2 position)
    {
       // Vector2 worldPosition = new Vector2(-mapSize/2 + mapSize * position.x, -mapSize / 2 + mapSize * position.y);
        //mappos += new Vector2i(mapResolution >> 1, mapResolution >> 1);
        //return mappos;
        return new Vector2(-mapSize / 2 + mapSize * position.x, -mapSize / 2 + mapSize * position.y);
    }

    public Vector2 WorldPositionToFogPositionNormalized(Vector3 position)
    {
        return (WorldPositionToModePosition(position) - mapOffset) / mapSize + new Vector2(0.5f, 0.5f);
    }

    // Returns a value between 0 (not in fog) and 255 (fully fogged)
    public byte GetFogValue(Vector3 position)
    {
        Vector2i mappos = WorldPositionToFogPosition(position);
        return texture.GetRawTextureData()[mappos.y * mapResolution + mappos.x];
    }

    public bool IsInCompleteFog(Vector3 position)
    {
        return GetFogValue(position) > 240;
    }

    public bool IsInPartialFog(Vector3 position)
    {
        return GetFogValue(position) > 20;
    }

    ColliderFogRectList GetExtendedColliders(FogFill fogfill, int layermask)
    {
        // quick check to see if all raycasts will hit something
        if (layermask == 0)
            return null;

        ColliderFogRectList colliderrects = null;
        if (mode == FogOfWarMode.Mode2D)
        {
            // is there anything overlapping with the area?
            Collider2D[] colliders = Physics2D.OverlapCircleAll(fogfill.worldPosition, fogfill.worldRadius, layermask);
            if (colliders.Length == 0)
                return null;

            // extend the colliders outwards away from the center
            colliderrects = new ColliderFogRectList(this);
            colliderrects.Add(colliders);
        }
        else//if (mode == FogOfWarMode.Mode3D)
        {
            // is there anything overlapping with the area?
            Collider[] colliders = Physics.OverlapSphere(fogfill.worldPosition, fogfill.worldRadius, layermask);
            if (colliders.Length == 0)
                return null;

            // extend the colliders outwards away from the center
            colliderrects = new ColliderFogRectList(this);
            colliderrects.Add(colliders);
        }

        colliderrects.ExtendToCircleEdge(fogfill.position, fogfill.radius);
        colliderrects.Optimise();
        return colliderrects.Count == 0 ? null : colliderrects;
    }

    public void Unfog(Vector3 position, float radius, float angle, Vector3 forward, int layermask = 0)
    {
        FogFill fogfill = new FogFill(this, position, radius, angle, forward);

        ColliderFogRectList colliderrects = GetExtendedColliders(fogfill, layermask);
        if (colliderrects == null)
        {
            fogfill.UnfogCircle(_values);
            return;
        }

        //if (colliderrects.Count != 1 || !colliderrects[0].ContainsCircle(fogfill.position, fogfill.radius))
        //    fogfill.UnfogCircle(_values, colliderrects);
        fogfill.UnfogCircleLineOfSight(_values, colliderrects, layermask);
    }

    public void Unfog(Rect rect)
    {
        Vector2i min = WorldPositionToFogPosition(rect.min);
        Vector2i max = WorldPositionToFogPosition(rect.max);
        for (int y = min.y; y < max.y; ++y)
        {
            for (int x = min.x; x < max.x; ++x)
                _values[y * mapResolution + x] = 0;
        }
    }

    void UpdateTexture()
    {
        texture.LoadRawTextureData(_values);
        texture.filterMode = filterMode;
        //texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply();

        byte partialfog = (byte)(partialFogAmount * 255);

        for (int y = 0; y < mapResolution; ++y)
        {
            for (int x = 0; x < mapResolution; ++x)
            {
                int index = y * mapResolution + x;
                if (_values[index] < partialfog)
                    _values[index] = partialfog;
            }
        }
    }

    void Update()
    {
        _nextUpdate -= Time.deltaTime;
        if (_nextUpdate > 0)
            return;

        _nextUpdate = updateFrequency;
        UpdateTexture();
    }

    // Returns the corner points relative to the camera's position (not rotation)
    static Matrix4x4 CalculateCameraFrustumCorners(Camera cam, Transform camtransform)
    {
        // Most of this was copied from the GlobalFog image effect standard asset due to the weird way to reconstruct the world position
        Matrix4x4 frustumCorners = Matrix4x4.identity;
        float camAspect = cam.aspect;
        float camNear = cam.nearClipPlane;
        float camFar = cam.farClipPlane;

        if (cam.orthographic)
        {
            float orthoSize = cam.orthographicSize;

            Vector3 far = camtransform.forward * camFar;
            Vector3 rightOffset = camtransform.right * (orthoSize * camAspect);
            Vector3 topOffset = camtransform.up * orthoSize;

            frustumCorners.SetRow(0, far + topOffset - rightOffset);
            frustumCorners.SetRow(1, far + topOffset + rightOffset);
            frustumCorners.SetRow(2, far - topOffset + rightOffset);
            frustumCorners.SetRow(3, far - topOffset - rightOffset);
        }
        else // perspective
        {
            float fovWHalf = cam.fieldOfView * 0.5f;
            float fovWHalfTan = Mathf.Tan(fovWHalf * Mathf.Deg2Rad);

            Vector3 toRight = camtransform.right * (camNear * fovWHalfTan * camAspect);
            Vector3 toTop = camtransform.up * (camNear * fovWHalfTan);

            Vector3 topLeft = (camtransform.forward * camNear - toRight + toTop);
            float camScale = topLeft.magnitude * camFar / camNear;

            topLeft.Normalize();
            topLeft *= camScale;

            Vector3 topRight = (camtransform.forward * camNear + toRight + toTop);
            topRight.Normalize();
            topRight *= camScale;

            Vector3 bottomRight = (camtransform.forward * camNear + toRight - toTop);
            bottomRight.Normalize();
            bottomRight *= camScale;

            Vector3 bottomLeft = (camtransform.forward * camNear - toRight - toTop);
            bottomLeft.Normalize();
            bottomLeft *= camScale;

            frustumCorners.SetRow(0, topLeft);
            frustumCorners.SetRow(1, topRight);
            frustumCorners.SetRow(2, bottomRight);
            frustumCorners.SetRow(3, bottomLeft);
        }
        return frustumCorners;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        RenderFog(source, destination, _camera, _transform);
    }

    public void RenderFog(RenderTexture source, RenderTexture destination, Camera cam, Transform camtransform)
    {
        if (clearFog)
        {
            RenderTexture temprendertex = new RenderTexture(source.width, source.height, source.depth);
            RenderFogFull(source, temprendertex, cam, camtransform);
            RenderClearFog(temprendertex, destination);
            Destroy(temprendertex);
        }
        else
            RenderFogFull(source, destination, cam, camtransform);
    }

    void RenderFogFull(RenderTexture source, RenderTexture destination, Camera cam, Transform camtransform)
    {
        _material.SetColor("_FogColor", fogColor);
        _material.SetMatrix("_FrustumCornersWS", CalculateCameraFrustumCorners(cam, camtransform));
        _material.SetVector("_CameraWS", camtransform.position);

        // orthographic is treated very differently in the shader, so we have to make sure it executes the right code
        if (cam.orthographic)
        {
            _material.DisableKeyword("CAMERA_PERSPECTIVE");
            _material.EnableKeyword("CAMERA_ORTHOGRAPHIC");

            Vector4 camdir = camtransform.forward;
            camdir.w = cam.nearClipPlane;
            _material.SetVector("_CameraDir", camdir);
        }
        else
        {
            _material.DisableKeyword("CAMERA_ORTHOGRAPHIC");
            _material.EnableKeyword("CAMERA_PERSPECTIVE");
        }

        // 2D and 3D are treated differently
        if (mode == FogOfWarMode.Mode2D)
        {
            _material.DisableKeyword("MODE3D");
            _material.EnableKeyword("MODE2D");
        }
        else// if (mode == FogOfWarMode.Mode3D)
        {
            _material.DisableKeyword("MODE2D");
            _material.EnableKeyword("MODE3D");
        }

        if (fogColorTexture != null)
        {
            _material.EnableKeyword("TEXTUREFOG");
            _material.SetTexture("_FogColorTex", fogColorTexture);
        }
        else
            _material.DisableKeyword("TEXTUREFOG");

        CustomGraphicsBlit(source, destination, _material);
    }

    void RenderClearFog(RenderTexture source, RenderTexture destination)
    {
        // create skybox camera
        Camera skyboxcamera = new GameObject("TempSkyboxFogCamera").AddComponent<Camera>();
        skyboxcamera.transform.parent = transform;
        skyboxcamera.transform.position = transform.position;
        skyboxcamera.transform.rotation = transform.rotation;
        skyboxcamera.fieldOfView = _camera.fieldOfView;
        skyboxcamera.clearFlags = CameraClearFlags.Skybox;
        skyboxcamera.targetTexture = new RenderTexture(source.width, source.height, source.depth);
        skyboxcamera.cullingMask = clearFogMask;
        skyboxcamera.orthographic = _camera.orthographic;
        skyboxcamera.orthographicSize = _camera.orthographicSize;
        skyboxcamera.rect = _camera.rect;

        // render skyboxcamera to texture
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = skyboxcamera.targetTexture;
        skyboxcamera.Render();
        Texture2D skyboximage = new Texture2D(skyboxcamera.targetTexture.width, skyboxcamera.targetTexture.height);
        skyboximage.ReadPixels(new Rect(0, 0, skyboxcamera.targetTexture.width, skyboxcamera.targetTexture.height), 0, 0);
        skyboximage.Apply();
        RenderTexture.active = currentRT;

        // overlay renders on eachother
        RenderTexture.active = destination;
        Material clearfogmat = new Material(clearFogShader);
        clearfogmat.SetTexture("_SkyboxTex", skyboximage);
        CustomGraphicsBlit(source, destination, clearfogmat);

        // ensure temp objects are destroyed
        Destroy(skyboxcamera.targetTexture);
        Destroy(skyboxcamera.gameObject);
        Destroy(clearfogmat);
        Destroy(skyboximage);
    }

    static void CustomGraphicsBlit(RenderTexture source, RenderTexture dest, Material material)
    {
        RenderTexture.active = dest;
        material.SetTexture("_MainTex", source);
        material.SetPass(0);

        GL.PushMatrix();
        GL.LoadOrtho();

        GL.Begin(GL.QUADS);

        GL.MultiTexCoord2(0, 0.0f, 0.0f);
        GL.Vertex3(0.0f, 0.0f, 3.0f); // BL

        GL.MultiTexCoord2(0, 1.0f, 0.0f);
        GL.Vertex3(1.0f, 0.0f, 2.0f); // BR

        GL.MultiTexCoord2(0, 1.0f, 1.0f);
        GL.Vertex3(1.0f, 1.0f, 1.0f); // TR

        GL.MultiTexCoord2(0, 0.0f, 1.0f);
        GL.Vertex3(0.0f, 1.0f, 0.0f); // TL

        GL.End();
        GL.PopMatrix();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (mode == FogOfWarMode.Mode2D)
            Gizmos.DrawWireCube(mapOffset, new Vector3(mapSize, mapSize, 0) * 0.5f);
        if (mode == FogOfWarMode.Mode3D)
            Gizmos.DrawWireCube(new Vector3(mapOffset.x, 0, mapOffset.y), new Vector3(mapSize, 0, mapSize) * 0.5f);
    }
}
