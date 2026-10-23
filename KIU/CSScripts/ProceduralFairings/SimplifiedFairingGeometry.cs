// Opt-in conservative collision panels for the CZ-10B PF pilot.
// Pure geometry: no Unity objects, scene access or changes to the visible fairing.
// Distributed under the upstream project's CC-BY-4.0 license.
using System;
using System.Collections.Generic;

namespace ProceduralFairings
{
    internal static class SimplifiedFairingGeometry
    {
        internal struct Point
        {
            public double R, Y;
            public Point(double r, double y) { R = r; Y = y; }
        }
        internal struct V3
        {
            public double X, Y, Z;
            public V3(double x, double y, double z) { X = x; Y = y; Z = z; }
            public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
            public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
            public static V3 operator *(V3 a, double s) => new V3(a.X * s, a.Y * s, a.Z * s);
            public double LengthSquared => Dot(this, this);
            public static double Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
            public static V3 Cross(V3 a, V3 b) => new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        }
        internal sealed class Settings
        {
            public int Sectors = 6;
            public int NosePanels = 4;
            public double SpanDegrees = 175;
            public double Thickness = .052;
            public double Clearance = .002;
            public double SeamGap = .01;
            public double MaxExpansionRatio = .06;
        }
        internal sealed class Panel
        {
            public V3[] Vertices;
            public int[] Triangles;
            public int Band, Sector;
        }
        internal struct F3
        {
            internal float X, Y, Z;
            internal F3(V3 v) { X = (float)v.X; Y = (float)v.Y; Z = (float)v.Z; }
        }
        internal sealed class FloatPanel
        {
            internal F3 Center;
            internal F3[] Vertices;
        }
        internal static FloatPanel CenterForFloat(Panel panel)
        {
            // Keep thin tip panels near their own origin during convex cooking.
            // Absolute 20+ metre vertex heights lose precision in Unity floats.
            V3 center = new V3();
            foreach (V3 v in panel.Vertices) center += v;
            center *= 1.0 / panel.Vertices.Length;
            var result = new FloatPanel { Center = new F3(center), Vertices = new F3[panel.Vertices.Length] };
            for (int i = 0; i < result.Vertices.Length; ++i) result.Vertices[i] = new F3(panel.Vertices[i] - center);
            return result;
        }
        internal sealed class Plan
        {
            public Panel[] Panels;
            public Point[] InnerBoundary, OuterBoundary;
            public int[] SelectedNativeEdges;
            public double MaximumRadialExpansion;
        }
        private struct Line
        {
            public double A, B;
            public int Edge;
            public Line(double a, double b, int edge) { A = a; B = b; Edge = edge; }
        }
        private sealed class Fit
        {
            public Line[] Lines;
            public Point[] Inner, Outer;
            public double Error;
        }
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);

        internal static bool TryBuild(Point[] input, Settings s, out Plan plan, out string reason)
        {
            plan = null;
            reason = null;
            if (input == null || input.Length < 3 || input.Length > 256) { reason = "profile-size"; return false; }
            if (s.Sectors < 3 || s.Sectors > 16 || s.NosePanels < 1 || s.NosePanels > 6 ||
                !Finite(s.SpanDegrees) || s.SpanDegrees <= 0 || s.SpanDegrees >= 180 ||
                !Finite(s.Thickness) || s.Thickness <= 0 || s.Thickness > 1 ||
                !Finite(s.Clearance) || s.Clearance < .0001 || s.Clearance > .05 ||
                !Finite(s.SeamGap) || s.SeamGap < .001 || s.SeamGap > .2 ||
                !Finite(s.MaxExpansionRatio) || s.MaxExpansionRatio <= 0 || s.MaxExpansionRatio > .5)
            { reason = "settings"; return false; }
            var points = new List<Point>();
            double radius = 0;
            foreach (Point p in input)
            {
                if (!Finite(p.R) || !Finite(p.Y) || p.R < 0 || p.R > 100 || Math.Abs(p.Y) > 1000)
                { reason = "nonfinite-or-out-of-range-profile"; return false; }
                radius = Math.Max(radius, p.R);
                if (points.Count > 0)
                {
                    Point last = points[points.Count - 1];
                    if (p.Y < last.Y - 1e-7) { reason = "nonmonotone-height"; return false; }
                    if (p.Y - last.Y < 1e-7)
                    {
                        if (Math.Abs(p.R - last.R) > 1e-7) { reason = "radial-step"; return false; }
                        continue;
                    }
                }
                points.Add(p);
            }
            if (points.Count < 3 || radius < .05 || Math.Abs(points[points.Count - 1].R) > 1e-5 ||
                Math.Abs(points[0].R - radius) > 1e-4 || points[points.Count - 1].Y - points[0].Y < .1)
            { reason = "requires-ordinary-barrel-and-nose"; return false; }
            Point[] shape = points.ToArray();
            var nose = new List<Line>();
            double previousSlope = 0;
            for (int i = 0; i + 1 < shape.Length; i++)
            {
                double a = (shape[i + 1].R - shape[i].R) / (shape[i + 1].Y - shape[i].Y);
                if (a > previousSlope + 1e-4 * (1 + Math.Abs(previousSlope)))
                { reason = "nonconcave-profile"; return false; }
                previousSlope = a;
                if (a >= -1e-8) continue;
                double b = shape[i].R - a * shape[i].Y;
                double supportedB = b;
                foreach (Point p in shape) supportedB = Math.Max(supportedB, p.R - a * p.Y);
                if (supportedB - b > .0025) { reason = "unsupported-profile-plane"; return false; }
                var line = new Line(a, supportedB, i);
                if (nose.Count > 0 && Math.Abs(a - nose[nose.Count - 1].A) < 1e-8 * (1 + Math.Abs(a)))
                    nose[nose.Count - 1] = line;
                else nose.Add(line);
            }
            if (nose.Count == 0) { reason = "no-nose"; return false; }
            // Bound selection work for unusually finely sampled presets. The original
            // shape itself is still used for all cavity and error checks.
            if (nose.Count > 16)
            {
                var reduced = new List<Line>();
                for (int i = 0; i < 16; i++) reduced.Add(nose[(int)Math.Round(i * (nose.Count - 1) / 15.0)]);
                nose = reduced;
            }
            int chosenCount = Math.Min(s.NosePanels, nose.Count);
            var chosen = new List<Line>();
            Fit best = null;
            Point[] visible = VisibleOuter(shape, s.Thickness);
            Search(nose, 0, chosenCount - 1, chosen, new Line(0, radius, 0), shape, visible, s, ref best);
            if (best == null) { reason = "no-valid-support-fit"; return false; }
            if (best.Error > radius * s.MaxExpansionRatio ||
                best.Outer[best.Outer.Length - 1].Y - visible[visible.Length - 1].Y > radius * s.MaxExpansionRatio)
            { reason = "collision-envelope-error-budget"; return false; }
            var panels = new List<Panel>();
            double halfAngle = s.SpanDegrees * Math.PI / 180 / (2 * s.Sectors);
            for (int band = 0; band < best.Lines.Length; band++)
            {
                var quad = new[] { best.Inner[band], best.Inner[band + 1], best.Outer[band + 1], best.Outer[band] };
                for (int sector = 0; sector < s.Sectors; sector++)
                {
                    double angle = -s.SpanDegrees * Math.PI / 360 + (2 * sector + 1) * halfAngle;
                    Panel panel = BuildPanel(quad, angle, halfAngle, s.SeamGap / 2);
                    if (panel == null) { reason = "degenerate-panel"; return false; }
                    Line line = best.Lines[band];
                    double norm = Math.Sqrt(1 + line.A * line.A);
                    foreach (V3 v in panel.Vertices)
                    {
                        double gap = (Math.Cos(angle) * v.X + Math.Sin(angle) * v.Z - line.A * v.Y - line.B) / norm;
                        if (gap < s.Clearance - 1e-6) { reason = "panel-crosses-cavity-support-plane"; return false; }
                    }
                    panel.Band = band; panel.Sector = sector; panels.Add(panel);
                }
            }
            var edges = new int[best.Lines.Length - 1];
            for (int i = 1; i < best.Lines.Length; i++) edges[i - 1] = best.Lines[i].Edge;
            plan = new Plan { Panels = panels.ToArray(), InnerBoundary = best.Inner, OuterBoundary = best.Outer,
                SelectedNativeEdges = edges, MaximumRadialExpansion = best.Error };
            return true;
        }

        private static void Search(List<Line> lines, int start, int remaining, List<Line> chosen,
            Line barrel, Point[] shape, Point[] visible, Settings s, ref Fit best)
        {
            if (remaining == 0)
            {
                var selected = new List<Line> { barrel }; selected.AddRange(chosen); selected.Add(lines[lines.Count - 1]);
                Fit fit = MakeFit(selected.ToArray(), shape[0].Y, visible, s);
                if (fit != null && (best == null || fit.Error < best.Error)) best = fit;
                return;
            }
            for (int i = start; i <= lines.Count - 1 - remaining; i++)
            {
                chosen.Add(lines[i]); Search(lines, i + 1, remaining - 1, chosen, barrel, shape, visible, s, ref best);
                chosen.RemoveAt(chosen.Count - 1);
            }
        }
        private static Fit MakeFit(Line[] lines, double startY, Point[] visible, Settings s)
        {
            Point[] inner = OffsetBoundary(lines, s.Clearance, startY);
            Point[] outer = OffsetBoundary(lines, s.Clearance + s.Thickness, startY);
            if (inner == null || outer == null) return null;
            double cosine = Math.Cos(s.SpanDegrees * Math.PI / 180 / (2 * s.Sectors));
            double error = 0;
            foreach (Point p in outer) error = Math.Max(error, p.R / cosine - Interpolate(visible, p.Y));
            foreach (Point p in visible) error = Math.Max(error, Interpolate(outer, p.Y) / cosine - p.R);
            return new Fit { Lines = lines, Inner = inner, Outer = outer, Error = error };
        }
        private static Point[] OffsetBoundary(Line[] lines, double offset, double startY)
        {
            var b = new double[lines.Length];
            for (int i = 0; i < b.Length; i++) b[i] = lines[i].B + offset * Math.Sqrt(1 + lines[i].A * lines[i].A);
            var result = new Point[lines.Length + 1]; result[0] = new Point(b[0], startY);
            for (int i = 1; i < lines.Length; i++)
            {
                double denominator = lines[i - 1].A - lines[i].A;
                if (denominator <= 1e-12) return null;
                double y = (b[i] - b[i - 1]) / denominator;
                double r = lines[i].A * y + b[i];
                if (!Finite(r) || !Finite(y) || r <= 0 || y <= result[i - 1].Y + 1e-8) return null;
                result[i] = new Point(r, y);
            }
            int last = lines.Length - 1;
            double tip = -b[last] / lines[last].A;
            if (!Finite(tip) || tip <= result[last].Y + 1e-8) return null;
            result[last + 1] = new Point(0, tip); return result;
        }
        private static Point[] VisibleOuter(Point[] shape, double thickness)
        {
            var result = new Point[shape.Length];
            for (int i = 0; i < shape.Length - 1; i++)
            {
                if (i == 0) { result[i] = new Point(shape[i].R + thickness, shape[i].Y); continue; }
                double dr = shape[i + 1].R - shape[i - 1].R, dy = shape[i + 1].Y - shape[i - 1].Y;
                double length = Math.Sqrt(dr * dr + dy * dy);
                result[i] = new Point(shape[i].R + thickness * dy / length, shape[i].Y - thickness * dr / length);
            }
            result[result.Length - 1] = new Point(0, shape[shape.Length - 1].Y + thickness);
            return result;
        }
        private static double Interpolate(Point[] p, double y)
        {
            if (y <= p[0].Y) return p[0].R;
            if (y >= p[p.Length - 1].Y) return p[p.Length - 1].R;
            for (int i = 1; i < p.Length; i++) if (y <= p[i].Y)
                return p[i - 1].R + (p[i].R - p[i - 1].R) * (y - p[i - 1].Y) / (p[i].Y - p[i - 1].Y);
            return 0;
        }

        private static Panel BuildPanel(Point[] profile, double angle, double halfAngle, double seamX)
        {
            var v = new V3[8];
            for (int i = 0; i < 4; i++) for (int side = 0; side < 2; side++)
            {
                double a = angle + (side == 0 ? -halfAngle : halfAngle), r = profile[i].R / Math.Cos(halfAngle);
                v[2 * i + side] = new V3(r * Math.Cos(a), profile[i].Y, r * Math.Sin(a));
            }
            var faces = new List<List<V3>> { new List<V3> { v[0], v[2], v[4], v[6] }, new List<V3> { v[1], v[3], v[5], v[7] } };
            for (int i = 0; i < 4; i++) { int j = (i + 1) % 4; faces.Add(new List<V3> { v[2 * i], v[2 * j], v[2 * j + 1], v[2 * i + 1] }); }
            var clipped = new List<List<V3>>(); var cut = new List<V3>();
            foreach (var face in faces)
            {
                var polygon = new List<V3>();
                for (int i = 0; i < face.Count; i++)
                {
                    V3 a = face[i], b = face[(i + 1) % face.Count]; bool aIn = a.X >= seamX, bIn = b.X >= seamX;
                    if (aIn) polygon.Add(a);
                    if (aIn != bIn)
                    {
                        V3 p = a + (b - a) * ((seamX - a.X) / (b.X - a.X)); p.X = seamX;
                        polygon.Add(p); AddUnique(cut, p);
                    }
                }
                CleanPolygon(polygon); if (polygon.Count >= 3) clipped.Add(polygon);
            }
            if (cut.Count >= 3)
            {
                V3 center = Mean(cut); cut.Sort((a, b) => Math.Atan2(a.Z - center.Z, a.Y - center.Y).CompareTo(Math.Atan2(b.Z - center.Z, b.Y - center.Y)));
                CleanPolygon(cut); if (cut.Count >= 3) clipped.Add(cut);
            }
            var vertices = new List<V3>();
            foreach (var face in clipped) foreach (V3 p in face) AddUnique(vertices, p);
            if (vertices.Count < 4) return null;
            V3 centroid = Mean(vertices); var triangles = new List<int>(); double volume = 0;
            foreach (var face in clipped)
            {
                V3 normal = V3.Cross(face[1] - face[0], face[2] - face[0]);
                if (V3.Dot(normal, Mean(face) - centroid) < 0) face.Reverse();
                int a = AddUnique(vertices, face[0]);
                for (int i = 1; i + 1 < face.Count; i++)
                {
                    int b = AddUnique(vertices, face[i]), c = AddUnique(vertices, face[i + 1]);
                    if (V3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).LengthSquared < 1e-22) continue;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    volume += V3.Dot(vertices[a] - centroid, V3.Cross(vertices[b] - centroid, vertices[c] - centroid)) / 6;
                }
            }
            if (volume <= 1e-12 || triangles.Count < 12 || triangles.Count > 255 * 3) return null;
            return new Panel { Vertices = vertices.ToArray(), Triangles = triangles.ToArray() };
        }
        private static void CleanPolygon(List<V3> p)
        {
            for (int i = p.Count - 1; i >= 0 && p.Count > 1; i--)
                if ((p[i] - p[(i + 1) % p.Count]).LengthSquared < 1e-16) p.RemoveAt(i);
            bool changed = true;
            while (changed && p.Count >= 3)
            {
                changed = false;
                for (int i = 0; i < p.Count; i++)
                {
                    V3 a = p[(i + p.Count - 1) % p.Count], b = p[i], c = p[(i + 1) % p.Count];
                    if (V3.Cross(b - a, c - b).LengthSquared < 1e-22) { p.RemoveAt(i); changed = true; break; }
                }
            }
        }
        private static int AddUnique(List<V3> p, V3 value)
        {
            for (int i = 0; i < p.Count; i++) if ((p[i] - value).LengthSquared < 1e-16) return i;
            p.Add(value); return p.Count - 1;
        }
        private static V3 Mean(List<V3> p)
        {
            var sum = new V3(); foreach (V3 v in p) sum += v; return sum * (1.0 / p.Count);
        }
    }
}
