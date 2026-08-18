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
        private Vector2 controlsScrollPosition;
        private Rect statsRect;
        private Rect controlsRect;
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
            statsRect = new Rect(18f, 18f, 350f, 210f);
            controlsRect = new Rect(width - 368f, 18f, 350f, height - 36f);
            selectedRect = new Rect(18f, height - 230f, 350f, 212f);

            DrawStatistics();
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
            GUILayout.BeginArea(new Rect(selectedRect.x + 16f, selectedRect.y + 12f, selectedRect.width - 32f, selectedRect.height - 24f));
            GUILayout.Label("INDIVIDUAL OBSERVATION", headerStyle);
            GUILayout.Space(8f);

            if (selectedCreature == null || selectedCreature.Genome == null)
            {
                GUILayout.Label("Click a body part to inspect an individual.", smallStyle);
                GUILayout.Label("The highlighted organism is selected for this generation.", smallStyle);
                GUILayout.EndArea();
                return;
            }

            CreatureGenome genome = selectedCreature.Genome;
            GUILayout.Label("Genome ID  " + genome.genomeId, labelStyle);
            GUILayout.Label("Fitness    " + selectedCreature.Fitness.ToString("0.000"), labelStyle);
            GUILayout.Label("BodyParts  " + selectedCreature.BodyPartCount, labelStyle);
            GUILayout.Label("Joints     " + selectedCreature.JointCount, labelStyle);
            GUILayout.Label("Generation " + genome.generation, labelStyle);
            GUILayout.Label("Parent ID  " + (string.IsNullOrEmpty(genome.parentId) ? "Founder" : genome.parentId), smallStyle);
            GUILayout.Label("Age        " + selectedCreature.AgeSeconds.ToString("0.0") + " s", smallStyle);
            GUILayout.Space(4f);
            GUILayout.Label("Distance   " + selectedCreature.CurrentDistance.ToString("0.000"), smallStyle);
            GUILayout.EndArea();
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
