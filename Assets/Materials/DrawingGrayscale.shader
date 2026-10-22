Shader "Hidden/MagicalLotus/DrawingGrayscale"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Brightness ("Brightness", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Brightness;

            fixed4 frag(v2f_img input) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, input.uv);
                fixed gray = dot(color.rgb, fixed3(0.299, 0.587, 0.114));
                return fixed4(gray * _Brightness, gray * _Brightness, gray * _Brightness, color.a);
            }
            ENDCG
        }
    }
}
