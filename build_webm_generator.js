const fs = require('fs');
const path = require('path');

const logoPath = 'c:/Marketing-wep/AMATORA/amatora-obs/logo.png';
const logoB64 = fs.readFileSync(logoPath).toString('base64');
const dataUri = 'data:image/png;base64,' + logoB64;

const htmlContent = `<!DOCTYPE html>
<html lang="uz">
<head>
  <meta charset="UTF-8">
  <title>AMATORA Stinger WEBM Generator</title>
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Montserrat:ital,wght@1,900&display=swap" rel="stylesheet">
  <style>
    body {
      background: #0f172a;
      color: #ffffff;
      font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      min-height: 100vh;
      margin: 0;
      padding: 20px;
    }
    h1 {
      margin-bottom: 10px;
      color: #38bdf8;
    }
    p {
      color: #9ca3af;
      margin-bottom: 20px;
      text-align: center;
    }
    .preview-box {
      position: relative;
      width: 960px;
      height: 540px;
      border: 3px solid #1e293b;
      border-radius: 12px;
      overflow: hidden;
      box-shadow: 0 20px 40px rgba(0,0,0,0.8);
      background: repeating-conic-gradient(#1e293b 0% 25%, #0f172a 0% 50%) 50% / 24px 24px;
    }
    canvas {
      width: 100%;
      height: 100%;
      display: block;
    }
    .controls {
      margin-top: 25px;
      display: flex;
      justify-content: center;
      align-items: center;
      position: relative;
      z-index: 9999;
      pointer-events: auto;
    }
    button {
      background: linear-gradient(135deg, #10b981 0%, #059669 100%);
      color: white;
      border: 2px solid #34d399;
      padding: 18px 36px;
      font-size: 19px;
      font-weight: 900;
      border-radius: 10px;
      cursor: pointer;
      box-shadow: 0 8px 25px rgba(16, 185, 129, 0.5);
      transition: all 0.2s ease;
      position: relative;
      z-index: 9999;
      pointer-events: auto;
    }
    button:hover {
      background: linear-gradient(135deg, #059669 0%, #047857 100%);
      transform: scale(1.03);
    }
    button:disabled {
      background: #475569;
      border-color: #64748b;
      cursor: not-allowed;
      box-shadow: none;
      transform: none;
    }
    #status {
      margin-top: 20px;
      font-size: 18px;
      font-weight: bold;
      color: #4ade80;
      text-align: center;
    }
  </style>
</head>
<body>

  <h1>⚽ AMATORA WEBM Stinger Generator</h1>
  <p>Sahifa ochilishi bilan video <b>AVTOMATIK YUKLANADI</b>. Agar yuklanmasa, yashil tugmani bosing!</p>

  <div class="preview-box">
    <canvas id="stingerCanvas" width="1920" height="1080"></canvas>
  </div>

  <div class="controls">
    <button id="startBtn">⚡ VIDEO YARATISH VA YUKLAB OLISH (AVTOMATIK)</button>
  </div>

  <div id="status">⏳ Video tayyorlanmoqda...</div>

  <script>
    const canvas = document.getElementById('stingerCanvas');
    const ctx = canvas.getContext('2d');
    const startBtn = document.getElementById('startBtn');
    const statusDiv = document.getElementById('status');

    // EMBEDDED ORIGINAL PNG LOGO DATA URI
    const logo = new Image();
    logo.src = "` + dataUri + `";

    const DURATION = 3.0; // 3.0 seconds animation
    const FPS = 60;
    const TOTAL_FRAMES = Math.round(DURATION * FPS);

    function getXPos(progress, offsetDelaySec = 0) {
      const adjustedTime = Math.max(0, (progress * DURATION) - offsetDelaySec);
      const p = Math.min(1, adjustedTime / (DURATION - offsetDelaySec));

      const width = canvas.width;
      const startX = -width * 2.4;
      const endX = width * 2.4;
      const centerX = 0;

      if (p <= 0.45) {
        const t = p / 0.45;
        const ease = 1 - Math.pow(1 - t, 3);
        return startX + (centerX - startX) * ease;
      } else if (p <= 0.55) {
        return centerX;
      } else {
        const t = (p - 0.55) / 0.45;
        const ease = Math.pow(t, 3);
        return centerX + (endX - centerX) * ease;
      }
    }

    function drawFrame(progress) {
      ctx.clearRect(0, 0, canvas.width, canvas.height);

      const tiltRad = 30 * Math.PI / 180;
      const barWidth = 520;
      const barHeight = canvas.height * 2.8;

      // 1. SECONDARY STRIPE
      const secX = getXPos(progress, 0.08);
      ctx.save();
      ctx.translate(canvas.width / 2 + secX, canvas.height / 2);
      ctx.rotate(tiltRad);

      const secGrad = ctx.createLinearGradient(-barWidth/2, -barHeight/2, barWidth/2, barHeight/2);
      secGrad.addColorStop(0, '#09122C');
      secGrad.addColorStop(0.5, '#1D4ED8');
      secGrad.addColorStop(1, '#0B193D');

      ctx.fillStyle = secGrad;
      ctx.fillRect(-barWidth/2, -barHeight/2, barWidth, barHeight);
      ctx.restore();

      // 2. PRIMARY STRIPE
      const primX = getXPos(progress, 0);
      ctx.save();
      ctx.translate(canvas.width / 2 + primX, canvas.height / 2);
      ctx.rotate(tiltRad);

      const primGrad = ctx.createLinearGradient(-barWidth/2, -barHeight/2, barWidth/2, barHeight/2);
      primGrad.addColorStop(0, '#030712');
      primGrad.addColorStop(0.4, '#09122C');
      primGrad.addColorStop(0.7, '#11224D');
      primGrad.addColorStop(1, '#050B1A');

      ctx.fillStyle = primGrad;
      ctx.shadowColor = 'rgba(0, 0, 0, 0.95)';
      ctx.shadowBlur = 60;
      ctx.fillRect(-barWidth/2, -barHeight/2, barWidth, barHeight);

      // Borders
      ctx.lineWidth = 4;
      ctx.strokeStyle = '#1D4ED8';
      ctx.shadowColor = 'rgba(29, 78, 216, 0.8)';
      ctx.shadowBlur = 40;

      ctx.beginPath();
      ctx.moveTo(-barWidth/2, -barHeight/2);
      ctx.lineTo(-barWidth/2, barHeight/2);
      ctx.stroke();

      ctx.beginPath();
      ctx.moveTo(barWidth/2, -barHeight/2);
      ctx.lineTo(barWidth/2, barHeight/2);
      ctx.stroke();

      // 3. CENTER LOGO & TITLE
      ctx.save();
      ctx.rotate(-tiltRad);

      // Draw EXACT ORIGINAL AMATORA PNG LOGO
      if (logo.complete && logo.naturalWidth !== 0) {
        const logoSize = 140;
        ctx.shadowColor = 'rgba(29, 78, 216, 0.9)';
        ctx.shadowBlur = 25;
        ctx.drawImage(logo, -logoSize/2, -110, logoSize, logoSize);
      }

      // Title "AMATORA" (Montserrat 900 italic)
      ctx.font = 'italic 900 52px "Montserrat", "Segoe UI", sans-serif';
      ctx.fillStyle = '#FFFFFF';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.shadowColor = 'rgba(29, 78, 216, 0.9)';
      ctx.shadowBlur = 25;
      
      const text = 'AMATORA';
      const letterSpacing = 10;
      let totalTextWidth = 0;
      for (let char of text) {
        totalTextWidth += ctx.measureText(char).width + letterSpacing;
      }
      totalTextWidth -= letterSpacing;

      let startTextX = -totalTextWidth / 2;
      for (let char of text) {
        const charWidth = ctx.measureText(char).width;
        ctx.fillText(char, startTextX + charWidth / 2, 70);
        startTextX += charWidth + letterSpacing;
      }

      ctx.restore();
      ctx.restore();
    }

    drawFrame(0.5);

    let isRecording = false;

    async function startRecordingProcess() {
      if (isRecording) return;
      isRecording = true;

      statusDiv.style.color = '#eab308';
      statusDiv.innerText = '🎥 Video 60 FPS tezlikda yozib olinmoqda (3.5s)...';
      startBtn.disabled = true;

      try {
        const stream = canvas.captureStream(FPS);

        let options = { mimeType: 'video/webm;codecs=vp9' };
        if (!MediaRecorder.isTypeSupported(options.mimeType)) {
          options = { mimeType: 'video/webm' };
        }

        const mediaRecorder = new MediaRecorder(stream, options);
        const chunks = [];

        mediaRecorder.ondataavailable = (e) => {
          if (e.data.size > 0) chunks.push(e.data);
        };

        mediaRecorder.onstop = () => {
          const blob = new Blob(chunks, { type: 'video/webm' });
          const url = URL.createObjectURL(blob);

          const a = document.createElement('a');
          a.href = url;
          a.download = 'amatora_stinger.webm';
          document.body.appendChild(a);
          a.click();
          document.body.removeChild(a);

          statusDiv.style.color = '#4ade80';
          statusDiv.innerText = '🎉 TAYYOR! "amatora_stinger.webm" yuklab olindi!';
          startBtn.disabled = false;
          startBtn.innerText = '⚡ QAYTA YUKLAB OLISH';
          isRecording = false;
        };

        mediaRecorder.start();

        let frame = 0;
        const interval = setInterval(() => {
          frame++;
          const progress = frame / TOTAL_FRAMES;
          drawFrame(progress);

          if (frame >= TOTAL_FRAMES) {
            clearInterval(interval);
            setTimeout(() => {
              mediaRecorder.stop();
            }, 500);
          }
        }, 1000 / FPS);
      } catch (err) {
        statusDiv.style.color = '#f87171';
        statusDiv.innerText = '❌ Xatolik: ' + err.message;
        startBtn.disabled = false;
        isRecording = false;
      }
    }

    startBtn.addEventListener('click', startRecordingProcess);

    // AUTO-START RECORDING AUTOMATICALLY AFTER 500ms
    window.addEventListener('load', () => {
      setTimeout(() => {
        startRecordingProcess();
      }, 600);
    });
  </script>
</body>
</html>`;

fs.writeFileSync('c:/Marketing-wep/AMATORA/amatora-obs/generate_webm.html', htmlContent);
console.log('Successfully updated generate_webm.html with auto-start recording!');
