using UnityEngine;
using System.Collections.Generic;
using RTS;
using System;

public class MiniMap : MonoBehaviour
{
    FogOfWar _fog;
    Texture2D _texture;
    GUIStyle _panelStyle;

    private Player player;

    private Vector2 positionOfMiniMapBegining;
    private int panelwidth;

    void Start()
    {
        player = GetComponentInParent<Player>();
        _fog = GetComponent<FogOfWar>();

    }

    void Update(){
        PositionMainCameraFromClickingOnMiniMap();
    }

    private void PositionMainCameraFromClickingOnMiniMap()
    {
        //if (IsOnMiniMap(Input.mousePosition))
        //{
          //  transform.position = Vector3(transform.position.x, transform.position.y, transform.position.z);
        //}
    }

    //private bool IsOnMiniMap(Vector3 mousePosition)
    //{
        //if (mousePosition.x > )
         //   return true;
        //return false;
    //}

    void DrawOnMap(string text, Vector3 position, int panelwidth)
    {
        Vector2i mappos = new Vector2i(_fog.WorldPositionToFogPositionNormalized(position) * (panelwidth - 10));
        GUI.Label(new Rect(Screen.width - panelwidth + mappos.x - 5, Screen.height - mappos.y - 5, 20, 20), text);
    }

    void OnGUI()
    {
        if (_texture == null)
        {
            _texture = new Texture2D(_fog.texture.width, _fog.texture.height);
            _texture.wrapMode = TextureWrapMode.Clamp;
        }

        if (_panelStyle == null)
        {
            Texture2D panelTex = new Texture2D(1, 1);
            panelTex.SetPixels32(new Color32[] { new Color32(255, 255, 255, 64) });
            panelTex.Apply();
            _panelStyle = new GUIStyle();
            _panelStyle.normal.background = panelTex;
        }

        byte[] original = _fog.texture.GetRawTextureData();
        Color32[] pixels = new Color32[original.Length];
        for (int i = 0; i < pixels.Length; ++i)
            pixels[i] = original[i] < 255 ? new Color32(0, 255, 50, 255) : new Color32(0, 0, 0, 255);
        _texture.SetPixels32(pixels);
        _texture.Apply();

        panelwidth = player.hud.GetORDERS_BAR_WIDTH;

        // draw panel
        //GUI.Box(new Rect(0, 0, panelwidth, Screen.height), "", _panelStyle);

        GUI.DrawTexture(new Rect(Screen.width - panelwidth, Screen.height - panelwidth, panelwidth, panelwidth), ResourceManager.GreyTexture);
        // draw map
        if (GUI.Button(new Rect(Screen.width - panelwidth, Screen.height - panelwidth, panelwidth, panelwidth), _texture))
{
            Vector2 worldPositionToGo = _fog.FogPositionToWorldPosition(GetMinimapRatinalHitPoint(Input.mousePosition));
            transform.position = new Vector3(worldPositionToGo.x, transform.position.y, worldPositionToGo.y);
        }

        DrawOnMap("C", transform.position, panelwidth);
}

    private Vector2 GetMinimapRatinalHitPoint(Vector3 mousePosition)
{
    return new Vector2((mousePosition.x - (Screen.width - panelwidth)) / panelwidth, mousePosition.y / panelwidth);
}
}