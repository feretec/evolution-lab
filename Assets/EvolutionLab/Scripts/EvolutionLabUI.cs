using System.Collections.Generic;
using UnityEngine;

namespace EvolutionLab
{
    /// <summary>
    /// Small runtime presentation layer for Prototype 1.
    /// IMGUI keeps the blank template scene free from serialized UI assets.
    /// </summary>
    public sealed class EvolutionLabUI : MonoBehaviour
    {
        private EvolutionSimulation simulation;
        private Creature selectedCreature;
        private GUIStyle panelStyle;
        private GUIStyle headerStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;
        private GUIStyle selectedButtonStyle;
        private GUIStyle graphStyle;
        private GUIStyle wrapStyle;
        private Material graphLineMaterial;
        private Vector2 controlsScrollPosition;
        private Rect statsRect;
        private Rect controlsRect;
        private Rect historyRect;
        private Rect selectedRect;

        public void Bind(EvolutionSimulation owner)
        {
            simulation = owner;
        }

        public void SetSelectedCreature(Creature creature)
        {
            selectedCreature = creature;
        }

        public bool IsPointerOverUI(Vector2 screenPosition)
        {
            Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return statsRect.Contains(guiPosition)
                || controlsRect.Contains(guiPosition)
                || historyRect.Contains(guiPosition)
                || selectedRect.Contains(guiPosition);
        }

        private void OnGUI()
        {
            if (simulation == null)
            {
                return;
            }

            EnsureStyles();
            float width = Mathf.Max(960f, Screen.width);
            float height = Mathf.Max(540f, Screen.height);
            statsRect = new Rect(18f, 18f, 350f, 190f);
            controlsRect = new Rect(width - 368f, 18f, 350f, height - 36f);
            float selectedHeight = Mathf.Clamp(height * 0.27f, 230f, 285f);
            selectedRect = new Rect(18f, height - selectedHeight - 18f, 350f, selectedHeight);
            float historyWidth = Mathf.Max(220f, controlsRect.x - statsRect.xMax - 24f);
            historyRect = new Rect(statsRect.xMax + 12f, statsRect.y, historyWidth, statsRect.height);

            DrawStatistics();
            DrawHistory();
            DrawControls();
            DrawSelectedCreature();
        }

        private void DrawStatistics()
        {
            GUI.Box(statsRect, GUIContent.none, panelStyle);
            GUILayout.BeginArea(new Rect(statsRect.x + 16f, statsRect.y + 12f, statsRect.width - 32f, statsRect.height - 24f));
            GUILayout.Label("EVOLUTION LAB", headerStyle);
            GUILayout.Label("MORPHOLOGICAL LOCOMOTION", smallStyle);
            GUILayout.Space(8f);
            GUILayout.Label("Generation     " + simulation.Generation, labelStyle);
            GUILayout.Label("Population     " + simulation.PopulationCount, labelStyle);
            GUILayout.Label("Best fitness   " + simulation.BestFitness.ToString("0.000"), labelStyle);
            GUILayout.Label("Average fitness " + simulation.AverageFitness.ToString("0.000"), labelStyle);
            GUILayout.Space(4f);
            GUILayout.Label(
                "Evaluation     " + simulation.EvaluationElapsed.ToString("0.0") + " / " + simulation.GenerationDuration.ToString("0.0") + " s",
                smallStyle);
            GUILayout.EndArea();
        }

        private void DrawHistory()
        {
            if (historyRect.height < 42f)
            {
                return;
            }

            GUI.Box(historyRect, GUIContent.none, panelStyle);
            GUI.Label(
                new Rect(historyRect.x + 16f, historyRect.y + 10f, historyRect.width - 32f, 24f),
                "EVOLUTION HISTORY",
                headerStyle);

            IReadOnlyList<GenerationRecord> records = simulation.GenerationHistory;
            int recordCount = records == null ? 0 : records.Count;
            GUI.Label(
                new Rect(historyRect.x + 16f, historyRect.y + 31f, historyRect.width - 32f, 18f),
                "Completed generations  " + recordCount,
                smallStyle);

            if (recordCount == 0)
            {
                GUI.Label(
                    new Rect(historyRect.x + 16f, historyRect.y + 58f, historyRect.width - 32f, 40f),
                    "The first generation is still being evaluated.",
                    smallStyle);
                return;
            }

            if (historyRect.height < 150f)
            {
                GenerationRecord latest = records[recordCount - 1];
                GUI.Label(
                    new Rect(historyRect.x + 16f, historyRect.y + 58f, historyRect.width - 32f, 40f),
                    "Latest  G" + latest.generation + "   Best " + latest.bestFitness.ToString("0.000")
                    + "   Avg " + latest.averageFitness.ToString("0.000"),
                    wrapStyle);
                return;
            }

            Rect graphRect = new Rect(
                historyRect.x + 16f,
                historyRect.y + 54f,
                historyRect.width - 32f,
                historyRect.height - 86f);
            GUI.Box(graphRect, GUIContent.none, graphStyle);
            if (Event.current.type == EventType.Repaint)
            {
                DrawFitnessGraph(graphRect, records);
            }

            GenerationRecord latestRecord = records[recordCount - 1];
            GUI.Label(
                new Rect(historyRect.x + 16f, historyRect.yMax - 28f, historyRect.width - 32f, 18f),
                "Best " + latestRecord.bestFitness.ToString("0.000")
                + "   Average " + latestRecord.averageFitness.ToString("0.000"),
                smallStyle);
        }

        private void DrawFitnessGraph(Rect graphRect, IReadOnlyList<GenerationRecord> records)
        {
            int count = records.Count;
            int start = Mathf.Max(0, count - 160);
            int last = count - 1;
            float maximum = 0.001f;
            for (int i = start; i < count; i++)
            {
                maximum = Mathf.Max(maximum, records[i].bestFitness, records[i].averageFitness);
            }

            DrawGraphLine(
                new Vector2(graphRect.x, graphRect.y + graphRect.height * 0.5f),
                new Vector2(graphRect.xMax, graphRect.y + graphRect.height * 0.5f),
                new Color(0.28f, 0.45f, 0.52f, 0.35f));
            DrawGraphLine(
                new Vector2(graphRect.x, graphRect.yMax - 1f),
                new Vector2(graphRect.xMax, graphRect.yMax - 1f),
                new Color(0.28f, 0.45f, 0.52f, 0.5f));

            for (int i = start + 1; i < count; i++)
            {
                Vector2 previousBest = GraphPoint(graphRect, i - 1, start, last, records[i - 1].bestFitness, maximum);
                Vector2 currentBest = GraphPoint(graphRect, i, start, last, records[i].bestFitness, maximum);
                Vector2 previousAverage = GraphPoint(graphRect, i - 1, start, last, records[i - 1].averageFitness, maximum);
                Vector2 currentAverage = GraphPoint(graphRect, i, start, last, records[i].averageFitness, maximum);
                DrawGraphLine(previousBest, currentBest, new Color(0.35f, 0.9f, 1f, 1f));
                DrawGraphLine(previousAverage, currentAverage, new Color(0.55f, 0.68f, 0.76f, 0.9f));
            }
        }

        private static Vector2 GraphPoint(
            Rect graphRect,
            int index,
            int firstIndex,
            int lastIndex,
            float value,
            float maximum)
        {
            float x = graphRect.x + Mathf.InverseLerp(firstIndex, Mathf.Max(firstIndex + 1, lastIndex), index) * graphRect.width;
            float y = graphRect.yMax - Mathf.Clamp01(value / maximum) * graphRect.height;
            return new Vector2(x, y);
        }

        private void DrawGraphLine(Vector2 from, Vector2 to, Color color)
        {
            if (graphLineMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null)
                {
                    return;
                }

                graphLineMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                graphLineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                graphLineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                graphLineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                graphLineMaterial.SetInt("_ZWrite", 0);
            }

            GL.PushMatrix();
            graphLineMaterial.SetPass(0);
            GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);
            GL.Begin(GL.LINES);
            GL.Color(color);
            GL.Vertex3(from.x, from.y, 0f);
            GL.Vertex3(to.x, to.y, 0f);
            GL.End();
            GL.PopMatrix();
        }

        private void DrawControls()
        {
            GUI.Box(controlsRect, GUIContent.none, panelStyle);
            Rect viewportRect = new Rect(
                controlsRect.x + 8f,
                controlsRect.y + 8f,
                controlsRect.width - 16f,
                controlsRect.height - 78f);
            Rect contentRect = new Rect(0f, 0f, viewportRect.width - 18f, 720f);
            controlsScrollPosition = GUI.BeginScrollView(viewportRect, controlsScrollPosition, contentRect);
            GUILayout.BeginArea(new Rect(8f, 8f, contentRect.width - 16f, contentRect.height - 16f));
            GUILayout.Label("SIMULATION CONTROLS", headerStyle);
            GUILayout.Space(8f);

            if (GUILayout.Button(simulation.IsPaused ? "Resume" : "Pause", buttonStyle, GUILayout.Height(32f)))
            {
                simulation.TogglePause();
            }

            GUILayout.Space(6f);
            GUILayout.Label("Simulation speed  " + simulation.SpeedLabel, smallStyle);
            GUILayout.BeginHorizontal();
            DrawSpeedButton("x1", 1f);
            DrawSpeedButton("x10", 10f);
            DrawSpeedButton("x100", 100f);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("Generation skip", smallStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1", buttonStyle, GUILayout.Height(28f)))
            {
                simulation.RequestGenerationSkip(1);
            }

            if (GUILayout.Button("+10", buttonStyle, GUILayout.Height(28f)))
            {
                simulation.RequestGenerationSkip(10);
            }

            if (GUILayout.Button("+100", buttonStyle, GUILayout.Height(28f)))
            {
                simulation.RequestGenerationSkip(100);
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("Queued: " + simulation.PendingGenerationSkips, smallStyle);

            GUILayout.Space(6f);
            GUILayout.Label("EVALUATION WINDOW", smallStyle);
            GUILayout.Label(
                "Generation duration  " + simulation.GenerationDuration.ToString("0.0") + " s",
                labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-5 s", buttonStyle, GUILayout.Height(25f)))
            {
                simulation.AdjustGenerationDuration(-5f);
            }

            if (GUILayout.Button("30 s", buttonStyle, GUILayout.Height(25f)))
            {
                simulation.SetGenerationDuration(30f);
            }

            if (GUILayout.Button("+5 s", buttonStyle, GUILayout.Height(25f)))
            {
                simulation.AdjustGenerationDuration(5f);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("JOINT PHYSICS", smallStyle);
            GUILayout.Label("Drive force       " + simulation.JointDriveForce.ToString("0"), labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Force -25", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustJointDriveForce(-25f);
            }

            if (GUILayout.Button("Force +25", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustJointDriveForce(25f);
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("Target speed     " + simulation.JointTargetSpeedDegrees.ToString("0") + " deg/s", labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Speed -40", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustJointTargetSpeedDegrees(-40f);
            }

            if (GUILayout.Button("Speed +40", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustJointTargetSpeedDegrees(40f);
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("Damping          " + simulation.JointDamping.ToString("0.0"), labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Damp -2", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustJointDamping(-2f);
            }

            if (GUILayout.Button("Damp +2", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustJointDamping(2f);
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("Settling         " + simulation.SettlingDuration.ToString("0.00") + " s", smallStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Settle -0.1", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustSettlingDuration(-0.1f);
            }

            if (GUILayout.Button("Settle +0.1", buttonStyle, GUILayout.Height(24f)))
            {
                simulation.AdjustSettlingDuration(0.1f);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("CAMERA", smallStyle);
            GUILayout.Label("WASD move / Q,E vertical / RMB look / Wheel dolly", smallStyle);
            GUILayout.EndArea();
            GUI.EndScrollView();

            float footerY = controlsRect.y + controlsRect.height - 62f;
            float footerWidth = (controlsRect.width - 42f) * 0.5f;
            if (GUI.Button(
                    new Rect(controlsRect.x + 16f, footerY, footerWidth, 26f),
                    "Reset camera view",
                    buttonStyle))
            {
                simulation.ResetCameraView();
            }

            if (GUI.Button(
                    new Rect(controlsRect.x + 26f + footerWidth, footerY, footerWidth, 26f),
                    "Reset experiment",
                    buttonStyle))
            {
                simulation.ResetSimulation();
            }
        }

        private void DrawSelectedCreature()
        {
            GUI.Box(selectedRect, GUIContent.none, panelStyle);
            if (selectedCreature != null
                && GUI.Button(
                    new Rect(selectedRect.x + selectedRect.width - 98f, selectedRect.y + 12f, 82f, 24f),
                    simulation.IsFollowingSelected ? "Unfollow" : "Follow",
                    buttonStyle))
            {
                simulation.ToggleFollowSelected();
            }

            GUILayout.BeginArea(new Rect(selectedRect.x + 16f, selectedRect.y + 12f, selectedRect.width - 32f, selectedRect.height - 24f));
            GUILayout.Label("INDIVIDUAL OBS.", headerStyle);
            GUILayout.Space(4f);

            if (selectedCreature == null || selectedCreature.Genome == null)
            {
                GUILayout.Label("Click a body part to inspect an individual.", smallStyle);
                GUILayout.Label("The highlighted organism is selected for this generation.", smallStyle);
                GUILayout.EndArea();
                return;
            }

            CreatureGenome genome = selectedCreature.Genome;
            GUILayout.Label("Genome ID  " + genome.genomeId, labelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Fitness " + selectedCreature.Fitness.ToString("0.000"), labelStyle, GUILayout.Width(156f));
            GUILayout.Label("Parts " + selectedCreature.BodyPartCount, labelStyle);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Joints " + selectedCreature.JointCount, labelStyle, GUILayout.Width(156f));
            GUILayout.Label("Generation " + genome.generation, labelStyle);
            GUILayout.EndHorizontal();
            GUILayout.Label("Parent ID  " + (string.IsNullOrEmpty(genome.parentId) ? "Founder" : genome.parentId), smallStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Age " + selectedCreature.AgeSeconds.ToString("0.0") + " s", smallStyle, GUILayout.Width(156f));
            GUILayout.Label("Distance " + selectedCreature.CurrentDistance.ToString("0.000"), smallStyle);
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            GUILayout.Label("ANCESTRY (newest to oldest)", smallStyle);
            GUILayout.Label(BuildAncestryLabel(), wrapStyle);
            GUILayout.EndArea();
        }

        private string BuildAncestryLabel()
        {
            IReadOnlyList<IndividualHistoryRecord> ancestry = simulation.SelectedAncestry;
            if (ancestry == null || ancestry.Count == 0)
            {
                return "Not recorded yet.";
            }

            string label = string.Empty;
            for (int i = 0; i < ancestry.Count; i++)
            {
                if (i > 0)
                {
                    label += "  >  ";
                }

                label += "G" + ancestry[i].generation;
            }

            return label;
        }

        private void DrawSpeedButton(string label, float speed)
        {
            GUIStyle style = Mathf.Approximately(simulation.SimulationSpeed, speed)
                ? selectedButtonStyle
                : buttonStyle;
            if (GUILayout.Button(label, style, GUILayout.Height(28f)))
            {
                simulation.SetSimulationSpeed(speed);
            }
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
            {
                return;
            }

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(16, 16, 12, 12),
                normal = { background = MakeTexture(new Color(0.035f, 0.055f, 0.075f, 0.94f)) }
            };
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.8f, 0.94f, 1f, 1f) }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = new Color(0.86f, 0.91f, 0.94f, 1f) }
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.58f, 0.72f, 0.78f, 1f) }
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };
            selectedButtonStyle = new GUIStyle(buttonStyle)
            {
                normal = { background = MakeTexture(new Color(0.12f, 0.42f, 0.52f, 1f)), textColor = Color.white }
            };
            graphStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(new Color(0.02f, 0.035f, 0.05f, 0.92f)) }
            };
            wrapStyle = new GUIStyle(smallStyle)
            {
                wordWrap = true
            };
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void OnDestroy()
        {
            DestroyStyleTexture(panelStyle);
            DestroyStyleTexture(selectedButtonStyle);
            DestroyStyleTexture(graphStyle);
            if (graphLineMaterial != null)
            {
                Destroy(graphLineMaterial);
            }
        }

        private static void DestroyStyleTexture(GUIStyle style)
        {
            if (style != null && style.normal != null && style.normal.background != null)
            {
                Destroy(style.normal.background);
            }
        }
    }
}
