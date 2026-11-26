// A colour wave behind the closing icon: "WebGL Shader" by Ali Imam (designali-in) on 21st.dev,
// https://21st.dev/@designali-in/components/web-gl-shader. Its shader's maths is kept as it is; it runs on plain WebGL
// instead of React and three.js. Changes for DispHub: the canvas is transparent where the shader is black (its light
// is the alpha), it fills the band behind the icon instead of the window, its time follows the clock rather than the
// frame count, it pauses off screen, and with reduced motion it draws one still frame.
(() => {
  'use strict';

  const vertexShader = `
    attribute vec3 position;
    void main() {
      gl_Position = vec4(position, 1.0);
    }
  `;

  const fragmentShader = `
    precision highp float;
    uniform vec2 resolution;
    uniform float time;
    uniform float xScale;
    uniform float yScale;
    uniform float distortion;

    void main() {
      vec2 p = (gl_FragCoord.xy * 2.0 - resolution) / min(resolution.x, resolution.y);

      float d = length(p) * distortion;

      float rx = p.x * (1.0 + d);
      float gx = p.x;
      float bx = p.x * (1.0 - d);

      float r = 0.05 / abs(p.y + sin((rx + time) * xScale) * yScale);
      float g = 0.05 / abs(p.y + sin((gx + time) * xScale) * yScale);
      float b = 0.05 / abs(p.y + sin((bx + time) * xScale) * yScale);

      // DispHub: light on transparent pixels instead of an opaque black backdrop
      vec3 c = clamp(vec3(r, g, b), 0.0, 1.0);
      gl_FragColor = vec4(c, max(c.r, max(c.g, c.b)));
    }
  `;

  const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  const compile = (gl, type, source) => {
    const shader = gl.createShader(type);
    gl.shaderSource(shader, source);
    gl.compileShader(shader);
    return gl.getShaderParameter(shader, gl.COMPILE_STATUS) ? shader : null;
  };

  function wave(container) {
    const canvas = document.createElement('canvas');
    canvas.setAttribute('aria-hidden', 'true');
    const gl = canvas.getContext('webgl', { alpha: true, premultipliedAlpha: true, antialias: false });
    if (!gl) return;
    const vs = compile(gl, gl.VERTEX_SHADER, vertexShader);
    const fs = compile(gl, gl.FRAGMENT_SHADER, fragmentShader);
    if (!vs || !fs) return;
    const program = gl.createProgram();
    gl.attachShader(program, vs);
    gl.attachShader(program, fs);
    gl.linkProgram(program);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) return;
    gl.useProgram(program);
    container.appendChild(canvas);

    // the original's two triangles over the whole canvas
    const position = [
      -1.0, -1.0, 0.0,
      1.0, -1.0, 0.0,
      -1.0, 1.0, 0.0,
      1.0, -1.0, 0.0,
      -1.0, 1.0, 0.0,
      1.0, 1.0, 0.0,
    ];
    gl.bindBuffer(gl.ARRAY_BUFFER, gl.createBuffer());
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(position), gl.STATIC_DRAW);
    const loc = gl.getAttribLocation(program, 'position');
    gl.enableVertexAttribArray(loc);
    gl.vertexAttribPointer(loc, 3, gl.FLOAT, false, 0, 0);

    const u = name => gl.getUniformLocation(program, name);
    const uResolution = u('resolution');
    const uTime = u('time');
    gl.uniform1f(u('xScale'), 1.0);
    gl.uniform1f(u('yScale'), 0.5);
    gl.uniform1f(u('distortion'), 0.05);

    let time = 0;
    let last = 0;
    let raf = 0;
    let visible = false;

    const draw = () => {
      const dpr = Math.min(window.devicePixelRatio || 1, 2);
      const w = Math.max(1, Math.round(container.clientWidth * dpr));
      const h = Math.max(1, Math.round(container.clientHeight * dpr));
      if (canvas.width !== w || canvas.height !== h) {
        canvas.width = w;
        canvas.height = h;
      }
      gl.viewport(0, 0, w, h);
      gl.uniform2f(uResolution, w, h);
      gl.uniform1f(uTime, time);
      gl.clearColor(0, 0, 0, 0);
      gl.clear(gl.COLOR_BUFFER_BIT);
      gl.drawArrays(gl.TRIANGLES, 0, 6);
    };

    const frame = now => {
      raf = 0;
      // the original adds 0.01 a frame, 0.6 a second at 60 frames
      time += Math.min(0.05, (now - last) / 1000 || 0) * 0.6;
      last = now;
      draw();
      if (visible && !document.hidden) raf = requestAnimationFrame(frame);
    };
    const wake = () => {
      if (reduce) {
        draw();
        return;
      }
      if (!raf && visible && !document.hidden) {
        last = performance.now();
        raf = requestAnimationFrame(frame);
      }
    };

    new IntersectionObserver(entries => {
      visible = entries.some(entry => entry.isIntersecting);
      wake();
    }).observe(container);
    new ResizeObserver(() => draw()).observe(container);
    document.addEventListener('visibilitychange', wake);
  }

  document.querySelectorAll('.wave').forEach(wave);
})();
