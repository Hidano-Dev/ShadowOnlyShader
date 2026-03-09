using NUnit.Framework;
using ShadowOnlyShader;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowOnlyShader.Tests.Runtime
{
    /// <summary>
    /// UPMパッケージの基盤構造（列挙型・インターフェース）の検証テスト
    /// </summary>
    public class PackageStructureTests
    {
        #region ProjectionMode Enum Tests

        [Test]
        public void ProjectionMode_HasOrthographicValue()
        {
            var mode = ProjectionMode.Orthographic;
            Assert.AreEqual(ProjectionMode.Orthographic, mode);
        }

        [Test]
        public void ProjectionMode_HasPerspectiveValue()
        {
            var mode = ProjectionMode.Perspective;
            Assert.AreEqual(ProjectionMode.Perspective, mode);
        }

        [Test]
        public void ProjectionMode_HasExactlyTwoValues()
        {
            var values = Enum.GetValues(typeof(ProjectionMode));
            Assert.AreEqual(2, values.Length, "ProjectionMode should have exactly 2 values: Orthographic and Perspective");
        }

        #endregion

        #region BlurQuality Enum Tests

        [Test]
        public void BlurQuality_HasLowValue()
        {
            var quality = BlurQuality.Low;
            Assert.AreEqual(BlurQuality.Low, quality);
        }

        [Test]
        public void BlurQuality_HasMidValue()
        {
            var quality = BlurQuality.Mid;
            Assert.AreEqual(BlurQuality.Mid, quality);
        }

        [Test]
        public void BlurQuality_HasHighValue()
        {
            var quality = BlurQuality.High;
            Assert.AreEqual(BlurQuality.High, quality);
        }

        [Test]
        public void BlurQuality_HasExactlyThreeValues()
        {
            var values = Enum.GetValues(typeof(BlurQuality));
            Assert.AreEqual(3, values.Length, "BlurQuality should have exactly 3 values: Low, Mid, High");
        }

        #endregion

        #region IShadowOnlyManager Interface Tests

        [Test]
        public void IShadowOnlyManager_InterfaceExists()
        {
            var type = typeof(IShadowOnlyManager);
            Assert.IsTrue(type.IsInterface, "IShadowOnlyManager should be an interface");
        }

        [Test]
        public void IShadowOnlyManager_HasVirtualLightsProperty()
        {
            var property = typeof(IShadowOnlyManager).GetProperty("VirtualLights");
            Assert.IsNotNull(property, "IShadowOnlyManager should have VirtualLights property");
            Assert.AreEqual(typeof(IReadOnlyList<IVirtualLight>), property.PropertyType);
            Assert.IsTrue(property.CanRead, "VirtualLights should be readable");
        }

        [Test]
        public void IShadowOnlyManager_HasAddVirtualLightMethod()
        {
            var method = typeof(IShadowOnlyManager).GetMethod("AddVirtualLight");
            Assert.IsNotNull(method, "IShadowOnlyManager should have AddVirtualLight method");
            Assert.AreEqual(typeof(IVirtualLight), method.ReturnType);
            Assert.AreEqual(0, method.GetParameters().Length, "AddVirtualLight should take no parameters");
        }

        [Test]
        public void IShadowOnlyManager_HasRemoveVirtualLightMethod()
        {
            var method = typeof(IShadowOnlyManager).GetMethod("RemoveVirtualLight");
            Assert.IsNotNull(method, "IShadowOnlyManager should have RemoveVirtualLight method");
            var parameters = method.GetParameters();
            Assert.AreEqual(1, parameters.Length);
            Assert.AreEqual(typeof(IVirtualLight), parameters[0].ParameterType);
        }

        [Test]
        public void IShadowOnlyManager_HasFloorRenderersProperty()
        {
            var property = typeof(IShadowOnlyManager).GetProperty("FloorRenderers");
            Assert.IsNotNull(property, "IShadowOnlyManager should have FloorRenderers property");
            Assert.AreEqual(typeof(IReadOnlyList<Renderer>), property.PropertyType);
        }

        [Test]
        public void IShadowOnlyManager_HasAddFloorRendererMethod()
        {
            var method = typeof(IShadowOnlyManager).GetMethod("AddFloorRenderer");
            Assert.IsNotNull(method, "IShadowOnlyManager should have AddFloorRenderer method");
            var parameters = method.GetParameters();
            Assert.AreEqual(1, parameters.Length);
            Assert.AreEqual(typeof(Renderer), parameters[0].ParameterType);
        }

        [Test]
        public void IShadowOnlyManager_HasRemoveFloorRendererMethod()
        {
            var method = typeof(IShadowOnlyManager).GetMethod("RemoveFloorRenderer");
            Assert.IsNotNull(method, "IShadowOnlyManager should have RemoveFloorRenderer method");
            var parameters = method.GetParameters();
            Assert.AreEqual(1, parameters.Length);
            Assert.AreEqual(typeof(Renderer), parameters[0].ParameterType);
        }

        [Test]
        public void IShadowOnlyManager_HasBlurQualityProperty()
        {
            var property = typeof(IShadowOnlyManager).GetProperty("BlurQuality");
            Assert.IsNotNull(property, "IShadowOnlyManager should have BlurQuality property");
            Assert.AreEqual(typeof(BlurQuality), property.PropertyType);
            Assert.IsTrue(property.CanRead);
            Assert.IsTrue(property.CanWrite);
        }

        [Test]
        public void IShadowOnlyManager_HasBlendMultiplierProperty()
        {
            var property = typeof(IShadowOnlyManager).GetProperty("BlendMultiplier");
            Assert.IsNotNull(property, "IShadowOnlyManager should have BlendMultiplier property");
            Assert.AreEqual(typeof(float), property.PropertyType);
            Assert.IsTrue(property.CanRead);
            Assert.IsTrue(property.CanWrite);
        }

        #endregion

        #region IVirtualLight Interface Tests

        [Test]
        public void IVirtualLight_InterfaceExists()
        {
            var type = typeof(IVirtualLight);
            Assert.IsTrue(type.IsInterface, "IVirtualLight should be an interface");
        }

        [Test]
        public void IVirtualLight_HasProjectionModeProperty()
        {
            var property = typeof(IVirtualLight).GetProperty("ProjectionMode");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(ProjectionMode), property.PropertyType);
            Assert.IsTrue(property.CanRead);
            Assert.IsTrue(property.CanWrite);
        }

        [Test]
        public void IVirtualLight_HasFieldOfViewProperty()
        {
            var property = typeof(IVirtualLight).GetProperty("FieldOfView");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(float), property.PropertyType);
            Assert.IsTrue(property.CanRead);
            Assert.IsTrue(property.CanWrite);
        }

        [Test]
        public void IVirtualLight_HasOrthographicSizeProperty()
        {
            var property = typeof(IVirtualLight).GetProperty("OrthographicSize");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(float), property.PropertyType);
            Assert.IsTrue(property.CanRead);
            Assert.IsTrue(property.CanWrite);
        }

        [Test]
        public void IVirtualLight_HasNearClipPlaneProperty()
        {
            var property = typeof(IVirtualLight).GetProperty("NearClipPlane");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(float), property.PropertyType);
        }

        [Test]
        public void IVirtualLight_HasFarClipPlaneProperty()
        {
            var property = typeof(IVirtualLight).GetProperty("FarClipPlane");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(float), property.PropertyType);
        }

        [Test]
        public void IVirtualLight_HasTextureResolutionProperty()
        {
            var property = typeof(IVirtualLight).GetProperty("TextureResolution");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(int), property.PropertyType);
        }

        [Test]
        public void IVirtualLight_HasShadowColorProperty()
        {
            var property = typeof(IVirtualLight).GetProperty("ShadowColor");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(Color), property.PropertyType);
        }

        [Test]
        public void IVirtualLight_HasShadowAlphaProperty()
        {
            var property = typeof(IVirtualLight).GetProperty("ShadowAlpha");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(float), property.PropertyType);
        }

        [Test]
        public void IVirtualLight_HasBlurRadiusProperty()
        {
            var property = typeof(IVirtualLight).GetProperty("BlurRadius");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(float), property.PropertyType);
        }

        [Test]
        public void IVirtualLight_HasBlurDistanceFactorProperty()
        {
            var property = typeof(IVirtualLight).GetProperty("BlurDistanceFactor");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(float), property.PropertyType);
        }

        [Test]
        public void IVirtualLight_HasHueShiftProperty()
        {
            var property = typeof(IVirtualLight).GetProperty("HueShift");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(float), property.PropertyType);
        }

        [Test]
        public void IVirtualLight_HasChromaticAberrationProperty()
        {
            var property = typeof(IVirtualLight).GetProperty("ChromaticAberration");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(float), property.PropertyType);
        }

        [Test]
        public void IVirtualLight_HasDepthBiasProperty()
        {
            var property = typeof(IVirtualLight).GetProperty("DepthBias");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(float), property.PropertyType);
        }

        [Test]
        public void IVirtualLight_HasNormalBiasProperty()
        {
            var property = typeof(IVirtualLight).GetProperty("NormalBias");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(float), property.PropertyType);
        }

        [Test]
        public void IVirtualLight_HasCasterRootProperty()
        {
            var property = typeof(IVirtualLight).GetProperty("CasterRoot");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(GameObject), property.PropertyType);
        }

        [Test]
        public void IVirtualLight_HasCasterRenderersProperty()
        {
            var property = typeof(IVirtualLight).GetProperty("CasterRenderers");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(IReadOnlyList<Renderer>), property.PropertyType);
            Assert.IsTrue(property.CanRead);
            Assert.IsFalse(property.CanWrite, "CasterRenderers should be read-only");
        }

        [Test]
        public void IVirtualLight_HasViewMatrixProperty()
        {
            var property = typeof(IVirtualLight).GetProperty("ViewMatrix");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(Matrix4x4), property.PropertyType);
            Assert.IsTrue(property.CanRead);
            Assert.IsFalse(property.CanWrite, "ViewMatrix should be read-only");
        }

        [Test]
        public void IVirtualLight_HasProjectionMatrixProperty()
        {
            var property = typeof(IVirtualLight).GetProperty("ProjectionMatrix");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(Matrix4x4), property.PropertyType);
            Assert.IsTrue(property.CanRead);
            Assert.IsFalse(property.CanWrite, "ProjectionMatrix should be read-only");
        }

        [Test]
        public void IVirtualLight_HasViewProjectionMatrixProperty()
        {
            var property = typeof(IVirtualLight).GetProperty("ViewProjectionMatrix");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(Matrix4x4), property.PropertyType);
            Assert.IsTrue(property.CanRead);
            Assert.IsFalse(property.CanWrite, "ViewProjectionMatrix should be read-only");
        }

        [Test]
        public void IVirtualLight_HasDepthRenderTextureProperty()
        {
            var property = typeof(IVirtualLight).GetProperty("DepthRenderTexture");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(RenderTexture), property.PropertyType);
            Assert.IsTrue(property.CanRead);
            Assert.IsFalse(property.CanWrite, "DepthRenderTexture should be read-only");
        }

        [Test]
        public void IVirtualLight_HasCollectRenderersMethod()
        {
            var method = typeof(IVirtualLight).GetMethod("CollectRenderers");
            Assert.IsNotNull(method);
            Assert.AreEqual(typeof(void), method.ReturnType);
            Assert.AreEqual(0, method.GetParameters().Length);
        }

        [Test]
        public void IVirtualLight_HasUpdateMatricesMethod()
        {
            var method = typeof(IVirtualLight).GetMethod("UpdateMatrices");
            Assert.IsNotNull(method);
            Assert.AreEqual(typeof(void), method.ReturnType);
            Assert.AreEqual(0, method.GetParameters().Length);
        }

        #endregion
    }
}
