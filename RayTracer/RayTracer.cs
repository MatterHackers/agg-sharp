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

using System;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.RayTracer.Light;
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
		public RayTracer()
		{
		}

		public AntiAliasing AntiAliasing { get; set; } = AntiAliasing.Medium;
		public ColorF[][] ColorBuffer { get; set; }

		public double[][] DepthBuffer { get; set; }

		public bool MultiThreaded { get; set; } = true;
		public Vector3[][] NormalBuffer { get; set; }

		public bool RenderDiffuse { get; set; } = true;
		public bool RenderHighlights { get; set; } = true;
		public bool RenderReflection { get; set; } = true;
		public bool RenderRefraction { get; set; } = true;
		public bool RenderShadow { get; set; } = true;
		public bool TraceWithRayBundles { get; set; } = false;

		public void AntiAliasScene(RectangleInt viewport, Scene scene, ColorF[][] imageBufferAsDoubles, int maxSamples)
		{
			if (MultiThreaded)
			{
				System.Threading.Tasks.Parallel.For(1, viewport.Height - 1, y => //
				{
					AntiAliasXSpan(viewport, scene, imageBufferAsDoubles, maxSamples, y);
				});
			}
			else
			{
				for (int y = 1; y < viewport.Height - 1; y++)
				{
					AntiAliasXSpan(viewport, scene, imageBufferAsDoubles, maxSamples, y);
				}
			}
		}

		public void CopyColorBufferToImage(ImageBuffer destImage, RectangleInt viewport)
		{
			if (destImage.BitDepth != 32)
			{
				throw new Exception("We can only render to 32 bit dest at the moment.");
			}

			Byte[] destBuffer = destImage.GetBuffer();

			viewport.Bottom = Math.Max(0, Math.Min(destImage.Height, viewport.Bottom));
			viewport.Top = Math.Max(0, Math.Min(destImage.Height, viewport.Top));

			if (MultiThreaded)
			{
				System.Threading.Tasks.Parallel.For(viewport.Bottom, viewport.Height, y =>
				{
					CopyColorXSpan(destImage, viewport, y, destBuffer);
				});
			}
			else
			{
				for (int y = viewport.Bottom; y < viewport.Height; y++)
				{
					CopyColorXSpan(destImage, viewport, y, destBuffer);
				}
			}

			destImage.MarkImageChanged();
		}

		public void CopyDepthBufferToImage(ImageBuffer destImage, RectangleInt viewport)
		{
			if (destImage.BitDepth != 32)
			{
				throw new Exception("We can only render to 32 bit dest at the moment.");
			}

			Byte[] destBuffer = destImage.GetBuffer();

			viewport.Bottom = Math.Max(0, Math.Min(destImage.Height, viewport.Bottom));
			viewport.Top = Math.Max(0, Math.Min(destImage.Height, viewport.Top));

			double minZ = 5000;
			double maxZ = 0;
			for (int y = viewport.Bottom; y < viewport.Height; y++)
			{
				for (int x = viewport.Left; x < viewport.Right; x++)
				{
					double depthAtXY = DepthBuffer[x][y];
					if (depthAtXY < 5000)
					{
						minZ = Math.Min(minZ, depthAtXY);
						maxZ = Math.Max(maxZ, depthAtXY);
					}
				}
			}

			double divisor = maxZ - minZ;

			if (MultiThreaded)
			{
				System.Threading.Tasks.Parallel.For(viewport.Bottom, viewport.Height, y => //
				{
					CopyDepthXSpan(destImage, viewport, y, destBuffer, minZ, divisor);
				});
			}
			else
			{
				for (int y = viewport.Bottom; y < viewport.Height; y++)
				{
					CopyDepthXSpan(destImage, viewport, y, destBuffer, minZ, divisor);
				}
				destImage.MarkImageChanged();
			}
		}

		public void CopyNoramlBufferToImage(ImageBuffer destImage, RectangleInt viewport)
		{
			if (destImage.BitDepth != 32)
			{
				throw new Exception("We can only render to 32 bit dest at the moment.");
			}

			Byte[] destBuffer = destImage.GetBuffer();

			viewport.Bottom = Math.Max(0, Math.Min(destImage.Height, viewport.Bottom));
			viewport.Top = Math.Max(0, Math.Min(destImage.Height, viewport.Top));

			for (int y = viewport.Bottom; y < viewport.Height; y++)
			{
				for (int x = viewport.Left; x < viewport.Right; x++)
				{
					int bufferOffset = destImage.GetBufferOffsetY(y);

					// we don't need to set this if we are anti-aliased
					int totalOffset = bufferOffset + x * 4;
					destBuffer[totalOffset++] = (byte)((NormalBuffer[x][y].X + 1) * 128);
					destBuffer[totalOffset++] = (byte)((NormalBuffer[x][y].Y + 1) * 128);
					destBuffer[totalOffset++] = (byte)((NormalBuffer[x][y].Z + 1) * 128);
					destBuffer[totalOffset] = 255;
				}
			}

			destImage.MarkImageChanged();
		}

		public ColorF CreateAndTraceSecondaryRays(IntersectInfo info, Ray ray, Scene scene, int depth)
		{
			// calculate ambient light
			ColorF infoColorAtHit = info.closestHitObject.GetColor(info);
			ColorF color = infoColorAtHit * scene.background.Ambience;
			color.alpha = infoColorAtHit.alpha;
			double shininess = Math.Pow(10, info.closestHitObject.Material.Gloss + 1);

			foreach (ILight light in scene.lights)
			{
				// calculate diffuse lighting
				Vector3 directiorFromHitToLight = light.Origin - info.HitPosition;
				double distanceToLight = directiorFromHitToLight.Length;
				Vector3 directiorFromHitToLightNormalized = directiorFromHitToLight.GetNormal();

				if (RenderDiffuse)
				{
					double L = Vector3.Dot(directiorFromHitToLightNormalized, info.normalAtHit);
					if (L > 0.0f)
					{
						color += infoColorAtHit * light.Illumination() * L;
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
						Ray reflectionRay = GetReflectionRay(info.HitPosition, info.normalAtHit, ray.directionNormal);
						IntersectInfo reflectionInfo = TracePrimaryRay(reflectionRay, scene);
						ColorF reflectionColorAtHit;// = reflectionInfo.closestHitObject.GetColor(reflectionInfo);
						if (reflectionInfo.hitType != IntersectionType.None && reflectionInfo.distanceToHit > 0)
						{
							// recursive call, this makes reflections expensive
							reflectionColorAtHit = CreateAndTraceSecondaryRays(reflectionInfo, reflectionRay, scene, depth + 1);
						}
						else // does not reflect an object, then reflect background color
						{
							reflectionColorAtHit = scene.background.Color;
							reflectionColorAtHit.Alpha0To1 = infoColorAtHit.alpha;
						}

						color = color.Blend(reflectionColorAtHit, info.closestHitObject.Material.Reflection);
					}

					//calculate refraction ray
					if (RenderRefraction && info.closestHitObject.Material.Transparency > 0)
					{
						Ray refractionRay = new Ray(info.HitPosition, ray.directionNormal, Ray.sameSurfaceOffset, double.MaxValue);  // GetRefractionRay(info.hitPosition, info.normalAtHit, ray.direction, info.closestHit.Material.Refraction);
						IntersectInfo refractionInfo = TracePrimaryRay(refractionRay, scene);
						ColorF refractionColorAtHit = refractionInfo.closestHitObject.GetColor(refractionInfo);
						if (refractionInfo.hitType != IntersectionType.None && refractionInfo.distanceToHit > 0)
						{
							// recursive call, this makes refractions expensive
							refractionColorAtHit = CreateAndTraceSecondaryRays(refractionInfo, refractionRay, scene, depth + 1);
						}
						else
						{
							refractionColorAtHit = scene.background.Color;
							refractionColorAtHit.Alpha0To1 = infoColorAtHit.alpha;
						}

						color = refractionColorAtHit.Blend(color, info.closestHitObject.Material.Transparency);
					}
				}

				IntersectInfo shadow = new IntersectInfo();
				if (RenderShadow)
				{
					// calculate shadow, create ray from intersection point to light
					Ray shadowRay = new Ray(info.HitPosition, directiorFromHitToLightNormalized, Ray.sameSurfaceOffset, double.MaxValue); // it may be useful to limit the length to the dist to the camera (but I doubt it LBB).
					shadowRay.isShadowRay = true;

					// if the normal at the closest hit is away from the shadow it is already it it's own shadow.
					if (Vector3.Dot(info.normalAtHit, directiorFromHitToLightNormalized) < 0)
					{
						shadow.hitType = IntersectionType.FrontFace;
						color *= 0.5;// +0.5 * Math.Pow(shadow.closestHit.Material.Transparency, 0.5); // Math.Pow(.5, shadow.HitCount);
						color.Alpha0To1 = infoColorAtHit.alpha;
					}
					else
					{
						// find any element in between intersection point and light
						shadow = TracePrimaryRay(shadowRay, scene);
						if (shadow.hitType != IntersectionType.None && shadow.closestHitObject != info.closestHitObject && shadow.distanceToHit < distanceToLight)
						{
							// only cast shadow if the found intersection is another
							// element than the current element
							color *= 0.5;// +0.5 * Math.Pow(shadow.closestHit.Material.Transparency, 0.5); // Math.Pow(.5, shadow.HitCount);
							color.Alpha0To1 = infoColorAtHit.alpha;
						}
					}
				}

				// only show highlights if it is not in the shadow of another object
				if (RenderHighlights && shadow.hitType == IntersectionType.None && info.closestHitObject.Material.Gloss > 0)
				{
					// only show Gloss light if it is not in a shadow of another element.
					// calculate Gloss lighting (Phong)
					Vector3 Lv = (info.HitPosition - light.Origin).GetNormal();
					Vector3 E = (ray.origin - info.HitPosition).GetNormal();
					Vector3 H = (E - Lv).GetNormal();

					double Glossweight = 0.0;
					Glossweight = Math.Pow(Math.Max(Vector3.Dot(info.normalAtHit, H), 0), shininess);
					color += light.Illumination() * (Glossweight);
				}
			}

			color.Clamp0To1();
			return color;
		}

		public ColorF FullyTraceRay(Ray ray, Scene scene, out IntersectInfo primaryInfo)
		{
			primaryInfo = TracePrimaryRay(ray, scene);
			if (primaryInfo.hitType != IntersectionType.None)
			{
				ColorF totalColor = CreateAndTraceSecondaryRays(primaryInfo, ray, scene, 0);
				return totalColor;
			}

			return scene.background.Color;
		}

		public void FullyTraceRayBundle(RayBundle rayBundle, IntersectInfo[] intersectionsForBundle, Scene scene)
		{
			TracePrimaryRayBundle(rayBundle, intersectionsForBundle, scene);
			for (int i = 0; i < rayBundle.rayArray.Length; i++)
			{
				try
				{
					IntersectInfo primaryInfo = TracePrimaryRay(rayBundle.rayArray[i], scene);
					if (intersectionsForBundle[i].hitType != IntersectionType.None)
					{
						intersectionsForBundle[i].totalColor = CreateAndTraceSecondaryRays(primaryInfo, rayBundle.rayArray[i], scene, 0);
					}
					else
					{
						intersectionsForBundle[i].totalColor = scene.background.Color;
					}
				}
				catch
				{
				}
			}
		}

		public void RayTraceScene(RectangleInt viewport, Scene scene)
		{
			int maxsamples = (int)AntiAliasing;

			//graphics2D.FillRectangle(viewport, RGBA_Floats.Black);

			if (ColorBuffer == null || ColorBuffer.Length < viewport.Width || ColorBuffer[0].Length < viewport.Height)
			{
				ColorBuffer = new ColorF[viewport.Width][];
				for (int i = 0; i < viewport.Width; i++)
				{
					ColorBuffer[i] = new ColorF[viewport.Height];
				}
				NormalBuffer = new Vector3[viewport.Width][];
				for (int i = 0; i < viewport.Width; i++)
				{
					NormalBuffer[i] = new Vector3[viewport.Height];
				}
				DepthBuffer = new double[viewport.Width][];
				for (int i = 0; i < viewport.Width; i++)
				{
					DepthBuffer[i] = new double[viewport.Height];
				}
			}

			if (TraceWithRayBundles)
			{
				int yStep = 8;
				int xStep = 8;
				for (int y = viewport.Bottom; y < viewport.Height; y += yStep)
				{
					for (int x = viewport.Left; x < viewport.Right; x += xStep)
					{
						try {
							int bundleWidth = Math.Min(xStep, viewport.Right - x);
							int bundleHeight = Math.Min(yStep, viewport.Top - y);
							FrustumRayBundle rayBundle = new FrustumRayBundle(bundleWidth * bundleHeight);
							IntersectInfo[] intersectionsForBundle = new IntersectInfo[bundleWidth * bundleHeight];

							// Calculate all the initial rays
							for (int rayY = 0; rayY < bundleHeight; rayY++)
							{
								for (int rayX = 0; rayX < bundleWidth; rayX++)
								{
									rayBundle.rayArray[rayX + rayY * bundleWidth] = scene.camera.GetRay(x + rayX, y + rayY);
									intersectionsForBundle[rayX + rayY * bundleWidth] = new IntersectInfo();
								}
							}

							// get a ray to find the origin (every ray comes from the camera and should have the same origin)
							rayBundle.CalculateFrustum(bundleWidth, bundleHeight, rayBundle.rayArray[0].origin);

							FullyTraceRayBundle(rayBundle, intersectionsForBundle, scene);

							// get the color data out of the traced rays
							for (int rayY = 0; rayY < bundleHeight; rayY++)
							{
								for (int rayX = 0; rayX < bundleWidth; rayX++)
								{
									ColorBuffer[x + rayX][y + rayY] = intersectionsForBundle[rayX + rayY * bundleWidth].totalColor;
								}
							}
						}
						catch
						{
						}
					}
				}
			}
			else
			{
				if (MultiThreaded)
				{
					System.Threading.Tasks.Parallel.For(viewport.Bottom, viewport.Height, y =>
					{
						TraceXSpan(viewport, scene, y);
					});
				}
				else
				{
					for (int y = viewport.Bottom; y < viewport.Height; y++)
					{
						TraceXSpan(viewport, scene, y);
					}
				}
			}

			if (AntiAliasing != AntiAliasing.None)
			{
				AntiAliasScene(viewport, scene, ColorBuffer, (int)AntiAliasing);
			}
		}

		public IntersectInfo TracePrimaryRay(Ray ray, Scene scene)
		{
			IntersectInfo primaryRayIntersection = new IntersectInfo();

			foreach (IPrimitive shapeToTest in scene.shapes)
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
				throw new Exception("You can only trace a ray bundle into a single shape, usually a BoundingVolumeHierachy.");
			}

			scene.shapes[0].GetClosestIntersections(rayBundle, 0, intersectionsForBundle);
		}

		private void AntiAliasXSpan(RectangleInt viewport, Scene scene, ColorF[][] imageBufferAsDoubles, int maxSamples, int y)
		{
			int fillY = viewport.Top - (viewport.Bottom + y);

			for (int x = 1; x < viewport.Width - 1; x++)
			{
				ColorF avg = (imageBufferAsDoubles[x - 1][y - 1] + imageBufferAsDoubles[x][y - 1] + imageBufferAsDoubles[x + 1][y - 1] +
							 imageBufferAsDoubles[x - 1][y] + imageBufferAsDoubles[x][y] + imageBufferAsDoubles[x + 1][y] +
							 imageBufferAsDoubles[x - 1][y + 1] + imageBufferAsDoubles[x][y + 1] + imageBufferAsDoubles[x + 1][y + 1]) / 9;

				// use a more accurate anti-aliasing method (MonteCarlo implementation)
				// this will fire multiple rays per pixel
				double sumOfDifferencesThreshold = .05; // TODO: figure out a good way to determine this.
				if (avg.SumOfDistances(imageBufferAsDoubles[x][y]) > sumOfDifferencesThreshold)
				{
					ColorF accumulatedColor = imageBufferAsDoubles[x][y];
					for (int i = 0; i < maxSamples; i++)
					{
						// get some 'random' samples
						double rx = Math.Sign(i % 4 - 1.5) * (IntNoise(x + y * viewport.Width * maxSamples * 2 + i) + 1) / 4;
						double ry = Math.Sign(i % 2 - 0.5) * (IntNoise(x + y * viewport.Width * maxSamples * 2 + 1 + i) + 1) / 4;

						double xp = x + rx;
						double yp = y + ry;

						Ray ray = scene.camera.GetRay(xp, yp);
						IntersectInfo primaryInfo;

						accumulatedColor += FullyTraceRay(ray, scene, out primaryInfo);
					}
					imageBufferAsDoubles[x][y] = accumulatedColor / (maxSamples + 1);
				}
			}
		}

		private void CopyColorXSpan(ImageBuffer destImage, RectangleInt viewport, int y, byte[] destBuffer)
		{
			for (int x = viewport.Left; x < viewport.Right; x++)
			{
				int bufferOffset = destImage.GetBufferOffsetY(y);

				// we don't need to set this if we are anti-aliased
				int totalOffset = bufferOffset + x * 4;
				destBuffer[totalOffset++] = (byte)ColorBuffer[x][y].Blue0To255;
				destBuffer[totalOffset++] = (byte)ColorBuffer[x][y].Green0To255;
				destBuffer[totalOffset++] = (byte)ColorBuffer[x][y].Red0To255;
				destBuffer[totalOffset] = (byte)ColorBuffer[x][y].Alpha0To255;
			}
		}

		private void CopyDepthXSpan(ImageBuffer destImage, RectangleInt viewport, int y, byte[] destBuffer, double minZ, double divisor)
		{
			for (int x = viewport.Left; x < viewport.Right; x++)
			{
				int bufferOffset = destImage.GetBufferOffsetY(y);

				// we don't need to set this if we are anti-aliased
				int totalOffset = bufferOffset + x * 4;
				double depthXY = DepthBuffer[x][y];
				double rangedDepth = (depthXY - minZ) / divisor;
				double clampedDepth = Math.Max(0, Math.Min(255, rangedDepth * 255));
				byte depthColor = (byte)(clampedDepth);
				destBuffer[totalOffset++] = depthColor;
				destBuffer[totalOffset++] = depthColor;
				destBuffer[totalOffset++] = depthColor;
				destBuffer[totalOffset] = 255;
			}
		}

		private Ray GetReflectionRay(Vector3 P, Vector3 N, Vector3 V)
		{
			double c1 = -Vector3.Dot(N, V);
			Vector3 Rl = V + (N * 2 * c1);
			return new Ray(P, Rl, Ray.sameSurfaceOffset, double.MaxValue);
		}

		private Ray GetRefractionRay(Vector3 P, Vector3 N, Vector3 V, double refraction)
		{
			int method = 0;
			switch (method)
			{
				case 0:
					return new Ray(P, V, Ray.sameSurfaceOffset, double.MaxValue);

				case 1:
					V = V * -1;
					double n = -0.55; // refraction constant for now
					if (n < 0 || n > 1)
					{
						return new Ray(P, V); // no refraction
					}
					break;

				case 2:
					double c1 = Vector3.Dot(N, V);
					double c2 = 1 - refraction * refraction * (1 - c1 * c1);
					if (c2 < 0)

						c2 = Math.Sqrt(c2);
					Vector3 T = (N * (refraction * c1 - c2) - V * refraction) * -1;
					T.Normalize();

					return new Ray(P, T); // no refraction
			}

			return new Ray(P, V, Ray.sameSurfaceOffset, double.MaxValue);
		}

		private double IntNoise(int x)
		{
			x = (x << 13) ^ x;
			return (1.0 - ((x * (x * x * 15731 + 789221) + 1376312589) & 0x7fffffff) / (int.MaxValue / 2.0));
		}

		private void TraceXSpan(RectangleInt viewport, Scene scene, int y)
		{
			for (int x = viewport.Left; x < viewport.Right; x++)
			{
				Ray ray = scene.camera.GetRay(x, y);

				IntersectInfo primaryInfo;
				ColorBuffer[x][y] = FullyTraceRay(ray, scene, out primaryInfo);

				if (false)
				{
					if (primaryInfo != null)
					{
						NormalBuffer[x][y] = primaryInfo.normalAtHit;
						DepthBuffer[x][y] = primaryInfo.distanceToHit;
					}
					else
					{
						NormalBuffer[x][y] = Vector3.UnitZ;
						DepthBuffer[x][y] = double.PositiveInfinity;
					}
				}
			}
		}
	}
}