// Copyright 2006 Herre Kuijpers - <herre@xs4all.nl>
//
// This source file(s) may be redistributed, altered and customized
// by any means PROVIDING the authors name and all copyright
// notices remain intact.
// THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED. USE IT AT YOUR OWN RISK. THE AUTHOR ACCEPTS NO
// LIABILITY FOR ANY DATA DAMAGE/LOSS THAT THIS PRODUCT MAY CAUSE.
//-----------------------------------------------------------------------
/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met: 

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer. 
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution. 

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies, 
either expressed or implied, of the FreeBSD Project.
*/

#define MULTI_THREAD

using System;
using System.Collections.Generic;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;

namespace MatterHackers.RayTracer
{
    public enum AntiAliasing
    {
        None = 0,
        Low = 4,
        Medium = 8,
        High = 16,
        VeryHigh = 32
    }

    public class RayTracer
    {
        public bool RenderDiffuse;
        public bool RenderHighlights;
        public bool RenderShadow;
        public bool RenderReflection;
        public bool RenderRefraction;
        public AntiAliasing AntiAliasing;

        public RayTracer()
            : this(AntiAliasing.Medium, true, true, true, true, true)
        {
        }

        public RayTracer(AntiAliasing antialiasing, bool renderDiffuse, bool renderHighlights, bool renderShadow, bool renderReflection, bool renderRefraction)
        {
            // run the ray trace unit tests.
            UnitTests.Run();

            RenderDiffuse = renderDiffuse;
            RenderHighlights = renderHighlights;
            RenderShadow = renderShadow;
            RenderReflection = renderReflection;
            RenderRefraction = renderRefraction;
            AntiAliasing = antialiasing;
        }

        private double IntNoise(int x)
        {
            x = (x << 13) ^ x;
            return (1.0 - ((x * (x * x * 15731 + 789221) + 1376312589) & 0x7fffffff) / (int.MaxValue / 2.0));
        }

        RGBA_Floats[][] imageBufferAsDoubles;
        public RGBA_Floats[][] RayTraceColorBuffer
        {
            get
            {
                return imageBufferAsDoubles;
            }
        }

        public bool traceWithRayBundles = false;
        public void RayTraceScene(Graphics2D graphics2D, RectangleInt viewport, Scene scene)
        {
            int maxsamples = (int)AntiAliasing;

            //graphics2D.FillRectangle(viewport, RGBA_Floats.Black);

            if (imageBufferAsDoubles == null || imageBufferAsDoubles.Length < viewport.Width || imageBufferAsDoubles[0].Length < viewport.Height)
            {
                imageBufferAsDoubles = new RGBA_Floats[viewport.Width][];
                for (int i = 0; i < viewport.Width; i++)
                {
                    imageBufferAsDoubles[i] = new RGBA_Floats[viewport.Height];
                }
            }

            IImageByte destImage = (IImageByte)graphics2D.DestImage;

            if (destImage.BitDepth != 32)
            {
                throw new Exception("We can only render to 32 bit dest at the moment.");
            }

            Byte[] destBuffer = destImage.GetBuffer();

            viewport.Bottom = Math.Max(0, Math.Min(destImage.Height, viewport.Bottom));
            viewport.Top = Math.Max(0, Math.Min(destImage.Height, viewport.Top));

#if MULTI_THREAD
            System.Threading.Tasks.Parallel.For(viewport.Bottom, viewport.Height, y => //  
#else
            for (int y = viewport.Bottom; y < viewport.Height; y++)
#endif
            {
                for (int x = viewport.Left; x < viewport.Right; x++)
                {
                    if (traceWithRayBundles)
                    {
                        int width = Math.Min(8, viewport.Right - x);
                        int height = Math.Min(8, viewport.Top - y);
                        FrustumRayBundle rayBundle = new FrustumRayBundle(width * height);
                        IntersectInfo[] intersectionsForBundle = new IntersectInfo[width * height];
                        for (int rayY = 0; rayY < height; rayY++)
                        {
                            for (int rayX = 0; rayX < width; rayX++)
                            {
                                rayBundle.rayArray[rayX + rayY * width] = scene.camera.GetRay(x + rayX, y + rayY);
                                intersectionsForBundle[rayX + rayY * width] = new IntersectInfo();
                            }
                        }

                        rayBundle.CalculateFrustum(width, height,  scene.camera.Origin);
                        
                        FullyTraceRayBundle(rayBundle, intersectionsForBundle, scene);

                        for (int rayY = 0; rayY < height; rayY++)
                        {
                            int bufferOffset = destImage.GetBufferOffsetY(y + rayY);

                            for (int rayX = 0; rayX < width; rayX++)
                            {
                                imageBufferAsDoubles[x + rayX][y + rayY] = intersectionsForBundle[rayX + rayY * width].totalColor;

                                // we don't need to set this if we are anti-aliased
                                int totalOffset = bufferOffset + (x + rayX) * 4;
                                destBuffer[totalOffset++] = (byte)imageBufferAsDoubles[x + rayX][y + rayY].Blue0To255;
                                destBuffer[totalOffset++] = (byte)imageBufferAsDoubles[x + rayX][y + rayY].Green0To255;
                                destBuffer[totalOffset++] = (byte)imageBufferAsDoubles[x + rayX][y + rayY].Red0To255;
                                destBuffer[totalOffset] = 255;
                            }
                        }
                        x += width - 1; // skip all the pixels we bundled
                        y += height - 1; // skip all the pixels we bundled
                    }
                    else
                    {
                        int bufferOffset = destImage.GetBufferOffsetY(y);

                        Ray ray = scene.camera.GetRay(x, y);

                        imageBufferAsDoubles[x][y] = FullyTraceRay(ray, scene);

                        // we don't need to set this if we are anti-aliased
                        int totalOffset = bufferOffset + x * 4;
                        destBuffer[totalOffset++] = (byte)imageBufferAsDoubles[x][y].Blue0To255;
                        destBuffer[totalOffset++] = (byte)imageBufferAsDoubles[x][y].Green0To255;
                        destBuffer[totalOffset++] = (byte)imageBufferAsDoubles[x][y].Red0To255;
                        destBuffer[totalOffset] = 255;
                    }
                }
            }
#if MULTI_THREAD
);
#endif
            if (AntiAliasing != AntiAliasing.None)
            {
                AntiAliasScene(graphics2D, viewport, scene, imageBufferAsDoubles, (int)AntiAliasing);
            }

            destImage.MarkImageChanged();
        }

        public void AntiAliasScene(Graphics2D graphics2D, RectangleInt viewport, Scene scene, RGBA_Floats[][] buffer, int maxSamples)
        {
            IImageByte destImage = (IImageByte)graphics2D.DestImage;

            if (destImage.BitDepth != 32)
            {
                throw new Exception("We can only render to 32 bit dest at the moment.");
            }

            Byte[] destBuffer = destImage.GetBuffer();

#if MULTI_THREAD
            System.Threading.Tasks.Parallel.For(1, viewport.Height - 1, y => //  
#else
            for (int y = 1; y < viewport.Height - 1; y++)
#endif
            {
                int fillY = viewport.Top - (viewport.Bottom + y);
                int bufferOffset = 0;
                if (y > 0 && y < destImage.Height)
                {
                    bufferOffset = destImage.GetBufferOffsetY(y);
                }

                for (int x = 1; x < viewport.Width - 1; x++)
                {
                    RGBA_Floats avg = (buffer[x - 1][y - 1] + buffer[x][y - 1] + buffer[x + 1][y - 1] +
                                 buffer[x - 1][y] + buffer[x][y] + buffer[x + 1][y] +
                                 buffer[x - 1][y + 1] + buffer[x][y + 1] + buffer[x + 1][y + 1]) / 9;

                    // use a more accurate antialasing method (MonteCarlo implementation)
                    // this will fire multiple rays per pixel
                    double sumOfDifferencesThreshold = .05; // TODO: figure out a good way to determin this.
                    if (avg.SumOfDistances(buffer[x][y]) > sumOfDifferencesThreshold)
                    {
                        RGBA_Floats accumulatedColor = buffer[x][y];
                        for (int i = 0; i < maxSamples; i++)
                        {
                            // get some 'random' samples
                            double rx = Math.Sign(i % 4 - 1.5) * (IntNoise(x + y * viewport.Width * maxSamples * 2 + i) + 1) / 4;
                            double ry = Math.Sign(i % 2 - 0.5) * (IntNoise(x + y * viewport.Width * maxSamples * 2 + 1 + i) + 1) / 4;

                            double xp = x + rx;
                            double yp = y + ry;

                            Ray ray = scene.camera.GetRay(xp, yp);
                            accumulatedColor += FullyTraceRay(ray, scene);
                        }
                        buffer[x][y] = accumulatedColor / (maxSamples + 1);

                        // this is the slow part of the painting algorithm, it can be greatly speed up
                        // by directly accessing the bitmap data
                        int fillX = viewport.Left + x;
                        int totalOffset = bufferOffset + fillX * 4;
                        destBuffer[totalOffset++] = (byte)buffer[x][y].Blue0To255;
                        destBuffer[totalOffset++] = (byte)buffer[x][y].Green0To255;
                        destBuffer[totalOffset++] = (byte)buffer[x][y].Red0To255;
                        destBuffer[totalOffset] = 255;
                    }
                }
            }
#if MULTI_THREAD
);
#endif

        }

        public RGBA_Floats FullyTraceRay(Ray ray, Scene scene)
        {
            IntersectInfo primaryInfo = TracePrimaryRay(ray, scene);
            if (primaryInfo.hitType != IntersectionType.None)
            {
                RGBA_Floats totalColor = CreateAndTraceSecondaryRays(primaryInfo, ray, scene, 0);
                return totalColor;
            }

            return scene.background.Color;
        }

        public void FullyTraceRayBundle(RayBundle rayBundle, IntersectInfo[] intersectionsForBundle, Scene scene)
        {
            TracePrimaryRayBundle(rayBundle, intersectionsForBundle, scene);
            for (int i = 0; i < rayBundle.rayArray.Length; i++)
            {
                IntersectInfo primaryInfo = TracePrimaryRay(rayBundle.rayArray[i], scene);
                if (intersectionsForBundle[i].hitType != IntersectionType.None)
                {
                    intersectionsForBundle[i].totalColor = CreateAndTraceSecondaryRays(primaryInfo, rayBundle.rayArray[i], scene, 0);
                }

                intersectionsForBundle[i].totalColor = scene.background.Color;
            }
        }

        public IntersectInfo TracePrimaryRay(Ray ray, Scene scene)
        {
            IntersectInfo primaryRayIntersection = new IntersectInfo();

            foreach (IRayTraceable shapeToTest in scene.shapes)
            {
                IntersectInfo info = shapeToTest.GetClosestIntersection(ray);
                if (info != null && info.hitType != IntersectionType.None && info.distanceToHit < primaryRayIntersection.distanceToHit && info.distanceToHit >= 0)
                {
                    primaryRayIntersection = info;
                }
            }
            return primaryRayIntersection;
        }

        public void TracePrimaryRayBundle(RayBundle rayBundle, IntersectInfo[] intersectionsForBundle, Scene scene)
        {
            if (scene.shapes.Count != 1)
            {
                throw new Exception("You can only trace a ray bundle into a sigle shape, usually a BoundingVolumeHierachy.");
            }

            scene.shapes[0].GetClosestIntersections(rayBundle, 0, intersectionsForBundle);
        }

        public RGBA_Floats CreateAndTraceSecondaryRays(IntersectInfo info, Ray ray, Scene scene, int depth)
        {
            // calculate ambient light
            RGBA_Floats infoColorAtHit = info.closestHitObject.GetColor(info);
            RGBA_Floats color = infoColorAtHit * scene.background.Ambience;
            double shininess = Math.Pow(10, info.closestHitObject.Material.Gloss + 1);

            foreach (Light light in scene.lights)
            {

                // calculate diffuse lighting
                Vector3 directiorFromHitToLight = light.Transform.Position - info.hitPosition;
                double distanceToLight = directiorFromHitToLight.Length;
                Vector3 directiorFromHitToLightNormalized = directiorFromHitToLight.GetNormal();

                if (RenderDiffuse)
                {
                    double L = Vector3.Dot(directiorFromHitToLightNormalized, info.normalAtHit);
                    if (L > 0.0f)
                    {
                        color += infoColorAtHit * light.Color * L;
                    }
                }


                // this is the max depth of raytracing.
                // increasing depth will calculate more accurate color, however it will
                // also take longer (exponentially)
                if (depth < 3)
                {
                    // calculate reflection ray
                    if (RenderReflection && info.closestHitObject.Material.Reflection > 0)
                    {
                        Ray reflectionRay = GetReflectionRay(info.hitPosition, info.normalAtHit, ray.direction);
                        IntersectInfo reflectionInfo = TracePrimaryRay(reflectionRay, scene);
                        RGBA_Floats reflectionColorAtHit;// = reflectionInfo.closestHitObject.GetColor(reflectionInfo);
                        if (reflectionInfo.hitType != IntersectionType.None && reflectionInfo.distanceToHit > 0)
                        {
                            // recursive call, this makes reflections expensive
                            reflectionColorAtHit = CreateAndTraceSecondaryRays(reflectionInfo, reflectionRay, scene, depth + 1);
                        }
                        else // does not reflect an object, then reflect background color
                        {
                            reflectionColorAtHit = scene.background.Color;
                        }

                        color = color.Blend(reflectionColorAtHit, info.closestHitObject.Material.Reflection);
                    }

                    //calculate refraction ray
                    if (RenderRefraction && info.closestHitObject.Material.Transparency > 0)
                    {
                        Ray refractionRay = new Ray(info.hitPosition, ray.direction, Ray.sameSurfaceOffset, double.MaxValue);  // GetRefractionRay(info.hitPosition, info.normalAtHit, ray.direction, info.closestHit.Material.Refraction);
                        IntersectInfo refractionInfo = TracePrimaryRay(refractionRay, scene);
                        RGBA_Floats refractionColorAtHit = refractionInfo.closestHitObject.GetColor(refractionInfo);
                        if (refractionInfo.hitType != IntersectionType.None && refractionInfo.distanceToHit > 0)
                        {
                            // recursive call, this makes refractions expensive
                            refractionColorAtHit = CreateAndTraceSecondaryRays(refractionInfo, refractionRay, scene, depth + 1);
                        }
                        else
                        {
                            refractionColorAtHit = scene.background.Color;
                        }

                        color = refractionColorAtHit.Blend(color, info.closestHitObject.Material.Transparency);
                    }
                }


                IntersectInfo shadow = new IntersectInfo();
                if (RenderShadow)
                {
                    // calculate shadow, create ray from intersection point to light
                    Ray shadowRay = new Ray(info.hitPosition, directiorFromHitToLightNormalized, Ray.sameSurfaceOffset, double.MaxValue); // it may be usefull to limit the legth to te dist to the camera (but I doubt it LBB).
                    shadowRay.isShadowRay = true;

                    // if the normal at the closest hit is away from the shadow it is already it it's own shadow.
                    if (Vector3.Dot(info.normalAtHit, directiorFromHitToLightNormalized) < 0)
                    {
                        shadow.hitType = IntersectionType.FrontFace;
                        color *= 0.5;// +0.5 * Math.Pow(shadow.closestHit.Material.Transparency, 0.5); // Math.Pow(.5, shadow.HitCount);
                    }
                    else
                    {
                        // find any element in between intersection point and light
                        shadow = TracePrimaryRay(shadowRay, scene);
                        if (shadow.hitType != IntersectionType.None && shadow.closestHitObject != info.closestHitObject && shadow.distanceToHit < distanceToLight)
                        {
                            // only cast shadow if the found interesection is another
                            // element than the current element
                            color *= 0.5;// +0.5 * Math.Pow(shadow.closestHit.Material.Transparency, 0.5); // Math.Pow(.5, shadow.HitCount);
                        }
                    }
                }

                // only show highlights if it is not in the shadow of another object
                if (RenderHighlights && shadow.hitType == IntersectionType.None && info.closestHitObject.Material.Gloss > 0)
                {
                    // only show Gloss light if it is not in a shadow of another element.
                    // calculate Gloss lighting (Phong)
                    Vector3 Lv = (info.hitPosition - light.Transform.Position).GetNormal();
                    Vector3 E = (scene.camera.Origin - info.hitPosition).GetNormal();
                    Vector3 H = (E - Lv).GetNormal();

                    double Glossweight = 0.0;
                    Glossweight = Math.Pow(Math.Max(Vector3.Dot(info.normalAtHit, H), 0), shininess);
                    color += light.Color * (Glossweight);
                }
            }

            color.Clamp0To1();
            return color;
        }

        private Ray GetReflectionRay(Vector3 P, Vector3 N, Vector3 V)
        {
            double c1 = -Vector3.Dot(N, V);
            Vector3 Rl = V + (N * 2 * c1);
            return new Ray(P, Rl, Ray.sameSurfaceOffset, double.MaxValue);
        }

        private Ray GetRefractionRay(Vector3 P, Vector3 N, Vector3 V, double refraction)
        {
#if true
            return new Ray(P, V, Ray.sameSurfaceOffset, double.MaxValue);
#else
            //V = V * -1;
            //double n = -0.55; // refraction constant for now
            //if (n < 0 || n > 1) return new Ray(P, V); // no refraction

            double c1 = N.Dot(V);
            double c2 = 1 - refraction * refraction * (1 - c1 * c1);
            if (c2 < 0)


                c2 = Math.Sqrt(c2);
            Vector3D T = (N * (refraction * c1 - c2) - V * refraction) * -1;
            T.Normalize();

            return new Ray(P, T); // no refraction
#endif
        }
    }
}
