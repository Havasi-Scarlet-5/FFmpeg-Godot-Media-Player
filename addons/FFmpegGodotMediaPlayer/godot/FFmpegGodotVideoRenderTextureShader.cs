using System;
using FFmpeg.AutoGen.Abstractions;
using Godot;

namespace FFmpegMediaPlayer.godot;

public abstract partial class FFmpegGodotVideoRenderTextureShader : RefCounted
{
    private protected TextureRect _texture = null;

    private protected FFmpegVideoDecoder _decoder = null;

    private protected ShaderMaterial _shaderMaterial = null;

    public abstract void SetHue(float value);

    public abstract void SetSaturation(float value);

    public abstract void SetLightness(float value);

    public abstract void SetContrast(float value);

    public abstract void SetTintColor(Color value);

    public abstract void SetChromaKeyEnable(bool value);

    public abstract void SetChromaKeyColor(Color value);

    public abstract void SetChromaKeyThreshold(float value);

    public abstract void SetChromaKeySmoothness(float value);

    private protected static float[,] GetColorSpaceMatrix(AVColorSpace colorSpace)
    {
        return colorSpace switch
        {
            // BT.601 (PAL/NTSC SDTV)
            AVColorSpace.AVCOL_SPC_BT470BG => new float[,]
            {
                { 1.0f,  0.0f,       1.402f    },
                { 1.0f, -0.344136f, -0.714136f },
                { 1.0f,  1.772f,     0.0f      }
            },
            AVColorSpace.AVCOL_SPC_SMPTE170M => new float[,]
            {
                { 1.0f,  0.0f,       1.402f    },
                { 1.0f, -0.344136f, -0.714136f },
                { 1.0f,  1.772f,     0.0f      }
            },
            // SMPTE 240M
            AVColorSpace.AVCOL_SPC_SMPTE240M => new float[,]
            {
                { 1.0f,  0.0f,       1.575f    },
                { 1.0f, -0.225f,    -0.500f    },
                { 1.0f,  1.826f,     0.0f      }
            },
            // BT.2020 (UHD/4K/8K)
            AVColorSpace.AVCOL_SPC_BT2020_NCL => new float[,]
            {
                { 1.0f,  0.0f,       1.4746f   },
                { 1.0f, -0.164553f, -0.571353f },
                { 1.0f,  1.8814f,    0.0f      }
            },
            AVColorSpace.AVCOL_SPC_BT2020_CL => new float[,]
            {
                { 1.0f,  0.0f,       1.4746f   },
                { 1.0f, -0.164553f, -0.571353f },
                { 1.0f,  1.8814f,    0.0f      }
            },
            // BT.709 (HDTV) - Default
            _ => new float[,]
            {
                { 1.0f,  0.0f,       1.5748f   },   // R
                { 1.0f, -0.1873f,   -0.4681f   },   // G
                { 1.0f,  1.8556f,    0.0f      }    // B
            }
        };
    }
}

public partial class FFmpegGodotVideoRenderTextureShaderYUV : FFmpegGodotVideoRenderTextureShader
{
    public FFmpegGodotVideoRenderTextureShaderYUV(TextureRect texture, FFmpegVideoDecoder decoder)
    {
        _texture = texture;

        _decoder = decoder;

        _shaderMaterial = new ShaderMaterial
        {
            Shader = (Shader)FFmpegGodotAutoLoad.Preloader.GetResource("YUVToRGB")
        };

        _texture.Material = _shaderMaterial;
    }

    public override void SetHue(float value)
    {
        _shaderMaterial?.SetShaderParameter("hue", value);
    }

    public override void SetSaturation(float value)
    {
        _shaderMaterial?.SetShaderParameter("saturation", value);
    }

    public override void SetLightness(float value)
    {
        _shaderMaterial?.SetShaderParameter("lightness", value);
    }

    public override void SetContrast(float value)
    {
        _shaderMaterial?.SetShaderParameter("contrast", value);
    }

    public override void SetTintColor(Color value)
    {
        _shaderMaterial?.SetShaderParameter("tint_color", value);
    }

    public override void SetChromaKeyEnable(bool value)
    {
        _shaderMaterial?.SetShaderParameter("chroma_key_enable", value);
    }

    public override void SetChromaKeyColor(Color value)
    {
        _shaderMaterial?.SetShaderParameter("chroma_key_color", value);
    }

    public override void SetChromaKeyThreshold(float value)
    {
        _shaderMaterial?.SetShaderParameter("chroma_key_threshold", value);
    }

    public override void SetChromaKeySmoothness(float value)
    {
        _shaderMaterial?.SetShaderParameter("chroma_key_smoothness", value);
    }

    private ImageTexture _yTexture = null;

    private ImageTexture _uTexture = null;

    private ImageTexture _vTexture = null;

    private Image _yImage = null;

    private Image _uImage = null;

    private Image _vImage = null;

    private bool recreatedTextures = true;

    public void UpdateYUVTexture(
        int yWidth,
        int uWidth,
        int vWidth,
        int height,
        int padding,
        ReadOnlySpan<byte> yData,
        ReadOnlySpan<byte> uData,
        ReadOnlySpan<byte> vData
    )
    {
        if (recreatedTextures)
        {
            _yImage = Image.CreateEmpty(yWidth, height, false, Image.Format.R8);

            _uImage = Image.CreateEmpty(uWidth, height / 2, false, Image.Format.R8);

            _vImage = Image.CreateEmpty(vWidth, height / 2, false, Image.Format.R8);

            _yTexture = ImageTexture.CreateFromImage(_yImage);

            _uTexture = ImageTexture.CreateFromImage(_uImage);

            _vTexture = ImageTexture.CreateFromImage(_vImage);

            _shaderMaterial?.SetShaderParameter("tex_y", _yTexture);

            _shaderMaterial?.SetShaderParameter("tex_u", _uTexture);

            _shaderMaterial?.SetShaderParameter("tex_v", _vTexture);

            float[,] m = GetColorSpaceMatrix(_decoder.ColorSpace);

            var basis = new Basis(
                new Vector3(m[0, 0], m[1, 0], m[2, 0]),
                new Vector3(m[0, 1], m[1, 1], m[2, 1]),
                new Vector3(m[0, 2], m[1, 2], m[2, 2])
            );

            _shaderMaterial?.SetShaderParameter("color_space_matrix", basis);

            var widthScale = padding > 0 ? 1.0f * (yWidth - padding - 1) / yWidth : 1.0f;

            _shaderMaterial?.SetShaderParameter("texture_scale", new Vector2(widthScale, 1.0f));

            recreatedTextures = false;
        }

        _yImage.SetData(yWidth, height, false, Image.Format.R8, yData);

        _uImage.SetData(uWidth, height / 2, false, Image.Format.R8, uData);

        _vImage.SetData(vWidth, height / 2, false, Image.Format.R8, vData);

        _yTexture.Update(_yImage);

        _uTexture.Update(_uImage);

        _vTexture.Update(_vImage);
    }
}