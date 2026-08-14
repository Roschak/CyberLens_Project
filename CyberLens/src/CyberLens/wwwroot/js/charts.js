// CyberLens D3 visualizations. Each render function is idempotent: it clears the
// target element and redraws, so Blazor can call it on data or theme change.
// Categorical palette is the validated dataviz reference set (light/dark steps).
(function () {
  const PAL = {
    light: ['#2a78d6', '#008300', '#e87ba4', '#eda100', '#1baf7a', '#eb6834', '#4a3aa7', '#e34948'],
    dark:  ['#3987e5', '#008300', '#d55181', '#c98500', '#199e70', '#d95926', '#9085e9', '#e66767']
  };
  const SENT = {
    light: { positive: '#1F8A4C', neutral: '#8b8474', negative: '#CC3B2E' },
    dark:  { positive: '#3FB56B', neutral: '#928b7c', negative: '#E8604F' }
  };

  function theme() { return document.documentElement.getAttribute('data-theme') || 'light'; }
  function palette() { return PAL[theme()]; }
  function sentColor(label) { return SENT[theme()][label] || SENT[theme()].neutral; }
  function css(v) { return getComputedStyle(document.documentElement).getPropertyValue(v).trim(); }
  function ink() { return css('--ink'); }
  function mute() { return css('--ink-mute'); }
  function line() { return css('--line'); }
  function surface() { return css('--surface'); }
  function accent() { return css('--accent'); }
  function font() { return 'IBM Plex Mono, monospace'; }

  let tip;
  function tipEl() {
    if (!tip) { tip = document.createElement('div'); tip.className = 'd3-tip'; tip.style.opacity = 0; document.body.appendChild(tip); }
    return tip;
  }
  function showTip(html, ev) { const t = tipEl(); t.innerHTML = html; t.style.opacity = 1; moveTip(ev); }
  function moveTip(ev) { const t = tipEl(); t.style.left = (ev.clientX + 14) + 'px'; t.style.top = (ev.clientY - 10) + 'px'; }
  function hideTip() { if (tip) tip.style.opacity = 0; }

  function clear(id) { const el = document.getElementById(id); if (el) el.innerHTML = ''; return el; }
  function size(el, h) { const w = Math.max(280, el.clientWidth || 600); return { w, h }; }

  // ---- Trend line with optional forecast (dashed) ----
  window.clTrendChart = function (id, points, opts) {
    const el = clear(id); if (!el || !points || !points.length) return;
    opts = opts || {};
    const h = opts.height || 260;
    const { w } = size(el, h);
    const m = { t: 14, r: 16, b: 30, l: 40 };
    const svg = d3.select(el).append('svg').attr('viewBox', `0 0 ${w} ${h}`);
    const data = points.map(p => ({ date: new Date(p.date || p.Date), value: p.value != null ? p.value : (p.count != null ? p.count : p.Value ?? p.Count), fc: p.isForecast ?? p.IsForecast ?? false }));
    const x = d3.scaleTime().domain(d3.extent(data, d => d.date)).range([m.l, w - m.r]);
    const y = d3.scaleLinear().domain([0, d3.max(data, d => d.value) * 1.15 || 10]).nice().range([h - m.b, m.t]);

    svg.append('g').attr('transform', `translate(0,${h - m.b})`).call(d3.axisBottom(x).ticks(6).tickFormat(d3.timeFormat('%d/%m')))
      .call(g => g.select('.domain').attr('stroke', line())).selectAll('text').attr('fill', mute()).style('font', '10px ' + font());
    svg.append('g').attr('transform', `translate(${m.l},0)`).call(d3.axisLeft(y).ticks(5))
      .call(g => g.select('.domain').remove()).call(g => g.selectAll('.tick line').attr('x2', w - m.l - m.r).attr('stroke', line()).attr('stroke-opacity', 0.25))
      .selectAll('text').attr('fill', mute()).style('font', '10px ' + font());

    const solid = data.filter(d => !d.fc);
    const fc = data.filter(d => d.fc);
    const lineGen = d3.line().x(d => x(d.date)).y(d => y(d.value)).curve(d3.curveMonotoneX);
    svg.append('path').datum(solid).attr('fill', 'none').attr('stroke', accent()).attr('stroke-width', 2.5).attr('d', lineGen);
    if (fc.length) {
      const bridge = solid.length ? [solid[solid.length - 1], ...fc] : fc;
      svg.append('path').datum(bridge).attr('fill', 'none').attr('stroke', accent()).attr('stroke-width', 2.5).attr('stroke-dasharray', '5,4').attr('opacity', 0.7).attr('d', lineGen);
    }
    // hover crosshair
    const focus = svg.append('circle').attr('r', 5).attr('fill', accent()).attr('stroke', ink()).attr('stroke-width', 2).style('opacity', 0);
    svg.append('rect').attr('x', m.l).attr('y', m.t).attr('width', w - m.l - m.r).attr('height', h - m.t - m.b).attr('fill', 'transparent')
      .on('mousemove', function (ev) {
        const mx = d3.pointer(ev)[0]; const dt = x.invert(mx);
        const i = d3.bisector(d => d.date).left(data, dt);
        const d = data[Math.min(i, data.length - 1)]; if (!d) return;
        focus.attr('cx', x(d.date)).attr('cy', y(d.value)).style('opacity', 1);
        showTip(`${d3.timeFormat('%d %b %Y')(d.date)}<br><b>${Math.round(d.value)}</b> post${d.fc ? ' (prediksi)' : ''}`, ev);
      }).on('mouseleave', () => { focus.style('opacity', 0); hideTip(); });
  };

  // ---- Multi-series category lines ----
  window.clSeriesChart = function (id, series, opts) {
    const el = clear(id); if (!el || !series || !series.length) return;
    opts = opts || {}; const h = opts.height || 280; const { w } = size(el, h);
    const m = { t: 14, r: 16, b: 30, l: 40 };
    const svg = d3.select(el).append('svg').attr('viewBox', `0 0 ${w} ${h}`);
    const pal = palette();
    const all = series.flatMap(s => (s.points || s.Points).map(p => ({ date: new Date(p.date || p.Date), value: p.count != null ? p.count : p.Count })));
    const x = d3.scaleTime().domain(d3.extent(all, d => d.date)).range([m.l, w - m.r]);
    const y = d3.scaleLinear().domain([0, d3.max(all, d => d.value) * 1.15 || 10]).nice().range([h - m.b, m.t]);
    svg.append('g').attr('transform', `translate(0,${h - m.b})`).call(d3.axisBottom(x).ticks(6).tickFormat(d3.timeFormat('%d/%m')))
      .call(g => g.select('.domain').attr('stroke', line())).selectAll('text').attr('fill', mute()).style('font', '10px ' + font());
    svg.append('g').attr('transform', `translate(${m.l},0)`).call(d3.axisLeft(y).ticks(5))
      .call(g => g.select('.domain').remove()).call(g => g.selectAll('.tick line').attr('x2', w - m.l - m.r).attr('stroke', line()).attr('stroke-opacity', 0.25))
      .selectAll('text').attr('fill', mute()).style('font', '10px ' + font());
    const lineGen = d3.line().x(d => x(new Date(d.date || d.Date))).y(d => y(d.count != null ? d.count : d.Count)).curve(d3.curveMonotoneX);
    series.forEach((s, i) => {
      svg.append('path').datum(s.points || s.Points).attr('fill', 'none').attr('stroke', pal[i % pal.length]).attr('stroke-width', 2.5).attr('d', lineGen);
    });
    renderLegend(el, series.map((s, i) => ({ label: s.name || s.Name, color: pal[i % pal.length] })));
  };

  // ---- Donut (sentiment) ----
  window.clDonut = function (id, slices, opts) {
    const el = clear(id); if (!el || !slices || !slices.length) return;
    opts = opts || {}; const h = opts.height || 240; const { w } = size(el, h);
    const r = Math.min(w, h) / 2 - 8;
    const svg = d3.select(el).append('svg').attr('viewBox', `0 0 ${w} ${h}`);
    const g = svg.append('g').attr('transform', `translate(${w / 2},${h / 2})`);
    const data = slices.map(s => ({ label: s.label || s.Label, count: s.count != null ? s.count : s.Count }));
    const total = d3.sum(data, d => d.count) || 1;
    const pie = d3.pie().value(d => d.count).sort(null);
    const arc = d3.arc().innerRadius(r * 0.58).outerRadius(r).padAngle(0.03).cornerRadius(3);
    const useSent = opts.sentiment;
    const pal = palette();
    g.selectAll('path').data(pie(data)).join('path')
      .attr('d', arc)
      .attr('fill', (d, i) => useSent ? sentColor(d.data.label) : pal[i % pal.length])
      .attr('stroke', surface()).attr('stroke-width', 2)
      .on('mousemove', (ev, d) => showTip(`${d.data.label}<br><b>${d.data.count}</b> (${Math.round(d.data.count / total * 100)}%)`, ev))
      .on('mouseleave', hideTip);
    g.append('text').attr('text-anchor', 'middle').attr('dy', '-2').style('font', '800 26px Bricolage Grotesque, sans-serif').attr('fill', ink()).text(total);
    g.append('text').attr('text-anchor', 'middle').attr('dy', '16').style('font', '10px ' + font()).attr('fill', mute()).text('TOTAL');
    renderLegend(el, data.map((d, i) => ({ label: `${d.label} (${d.count})`, color: useSent ? sentColor(d.label) : pal[i % pal.length] })));
  };

  // ---- Horizontal bars ----
  window.clBars = function (id, items, opts) {
    const el = clear(id); if (!el || !items || !items.length) return;
    opts = opts || {}; const { w } = size(el);
    const rowH = opts.rowH || 26; const m = { t: 6, r: 40, b: 6, l: opts.labelW || 130 };
    const h = m.t + m.b + items.length * rowH;
    const svg = d3.select(el).append('svg').attr('viewBox', `0 0 ${w} ${h}`);
    const data = items.map(it => ({ label: it.label || it.Label || it.name || it.Name || it.word || it.Word, value: it.value != null ? it.value : (it.count != null ? it.count : it.Count), color: it.color || it.Color }));
    const x = d3.scaleLinear().domain([0, d3.max(data, d => d.value) || 1]).range([m.l, w - m.r]);
    const pal = palette();
    const g = svg.append('g');
    const rows = g.selectAll('g').data(data).join('g').attr('transform', (d, i) => `translate(0,${m.t + i * rowH})`);
    rows.append('text').attr('x', m.l - 8).attr('y', rowH / 2).attr('dy', '0.35em').attr('text-anchor', 'end').attr('fill', ink()).style('font', '600 12px ' + font()).text(d => d.label.length > 18 ? d.label.slice(0, 17) + '…' : d.label);
    rows.append('rect').attr('x', m.l).attr('y', 3).attr('height', rowH - 8).attr('rx', 3).attr('width', d => x(d.value) - m.l)
      .attr('fill', (d, i) => d.color || pal[i % pal.length]).attr('stroke', ink()).attr('stroke-width', 1.5)
      .on('mousemove', (ev, d) => showTip(`${d.label}<br><b>${d.value}</b>`, ev)).on('mouseleave', hideTip);
    rows.append('text').attr('x', d => x(d.value) + 6).attr('y', rowH / 2).attr('dy', '0.35em').attr('fill', mute()).style('font', '11px ' + font()).text(d => d.value);
  };

  // ---- Word cloud (weighted, spiral) ----
  window.clWordCloud = function (id, words, opts) {
    const el = clear(id); if (!el || !words || !words.length) return;
    opts = opts || {}; const h = opts.height || 300; const { w } = size(el, h);
    const svg = d3.select(el).append('svg').attr('viewBox', `0 0 ${w} ${h}`);
    const data = words.slice(0, 40).map(x => ({ text: (x.word || x.Word || '').replace(/^#/, ''), size: x.count != null ? x.count : x.Count }));
    const max = d3.max(data, d => d.size) || 1, min = d3.min(data, d => d.size) || 1;
    const fs = d3.scaleSqrt().domain([min, max]).range([13, 46]);
    const pal = palette();
    d3.layout.cloud().size([w, h]).words(data.map(d => ({ text: d.text, size: fs(d.size), count: d.size })))
      .padding(3).rotate(() => (Math.random() < 0.7 ? 0 : 90)).font('Bricolage Grotesque').fontSize(d => d.size)
      .on('end', words => {
        svg.append('g').attr('transform', `translate(${w / 2},${h / 2})`).selectAll('text').data(words).join('text')
          .style('font-family', 'Bricolage Grotesque, sans-serif').style('font-weight', 700)
          .style('font-size', d => d.size + 'px').attr('fill', (d, i) => pal[i % pal.length]).attr('text-anchor', 'middle')
          .attr('transform', d => `translate(${d.x},${d.y}) rotate(${d.rotate})`).text(d => d.text)
          .style('cursor', 'default')
          .on('mousemove', (ev, d) => showTip(`${d.text}<br><b>${d.count}</b> mention`, ev)).on('mouseleave', hideTip);
      }).start();
  };

  // ---- Force-directed entity network ----
  window.clNetwork = function (id, graph, opts) {
    const el = clear(id); if (!el || !graph || !graph.nodes) return;
    opts = opts || {}; const h = opts.height || 460; const { w } = size(el, h);
    const svg = d3.select(el).append('svg').attr('viewBox', `0 0 ${w} ${h}`);
    const nodes = graph.nodes.map(n => ({ id: n.id ?? n.Id, name: n.name || n.Name, kind: n.kind || n.Kind, mentions: n.mentions ?? n.Mentions }));
    const links = graph.links.map(l => ({ source: l.source ?? l.Source, target: l.target ?? l.Target, weight: l.weight ?? l.Weight }));
    const kinds = ['Person', 'Organization', 'Hashtag', 'Location', 'Account'];
    const pal = palette();
    const color = k => pal[Math.max(0, kinds.indexOf(k)) % pal.length];
    const r = d3.scaleSqrt().domain([1, d3.max(nodes, d => d.mentions) || 1]).range([6, 26]);
    const sim = d3.forceSimulation(nodes)
      .force('link', d3.forceLink(links).id(d => d.id).distance(70).strength(0.3))
      .force('charge', d3.forceManyBody().strength(-140))
      .force('center', d3.forceCenter(w / 2, h / 2))
      .force('collide', d3.forceCollide(d => r(d.mentions) + 4));
    const link = svg.append('g').attr('stroke', line()).attr('stroke-opacity', 0.4).selectAll('line').data(links).join('line').attr('stroke-width', d => Math.sqrt(d.weight));
    const node = svg.append('g').selectAll('g').data(nodes).join('g').style('cursor', 'grab').call(drag(sim));
    node.append('circle').attr('r', d => r(d.mentions)).attr('fill', d => color(d.kind)).attr('stroke', ink()).attr('stroke-width', 2)
      .on('mousemove', (ev, d) => showTip(`${d.name}<br>${d.kind} · <b>${d.mentions}</b> mention`, ev)).on('mouseleave', hideTip);
    node.append('text').text(d => d.name.length > 14 ? d.name.slice(0, 13) + '…' : d.name).attr('x', d => r(d.mentions) + 4).attr('y', 4).attr('fill', ink()).style('font', '600 11px ' + font()).style('pointer-events', 'none');
    sim.on('tick', () => {
      link.attr('x1', d => d.source.x).attr('y1', d => d.source.y).attr('x2', d => d.target.x).attr('y2', d => d.target.y);
      node.attr('transform', d => `translate(${d.x},${d.y})`);
    });
    renderLegend(el, kinds.map((k, i) => ({ label: k, color: pal[i % pal.length] })));
    function drag(sim) {
      return d3.drag()
        .on('start', (ev, d) => { if (!ev.active) sim.alphaTarget(0.3).restart(); d.fx = d.x; d.fy = d.y; })
        .on('drag', (ev, d) => { d.fx = ev.x; d.fy = ev.y; })
        .on('end', (ev, d) => { if (!ev.active) sim.alphaTarget(0); d.fx = null; d.fy = null; });
    }
  };

  // ---- Geo bubble map (equirectangular, no external topojson) ----
  window.clGeoMap = function (id, points, opts) {
    const el = clear(id); if (!el) return;
    opts = opts || {}; const h = opts.height || 420; const { w } = size(el, h);
    const svg = d3.select(el).append('svg').attr('viewBox', `0 0 ${w} ${h}`);
    // Focus on SE Asia / global spread using a simple linear lat-lon projection.
    const lonExt = [90, 145], latExt = [-12, 25];
    const useGlobal = (points || []).some(p => (p.lon ?? p.Lon) < 90 || (p.lon ?? p.Lon) > 145 || (p.lat ?? p.Lat) > 25 || (p.lat ?? p.Lat) < -12);
    const lonD = useGlobal ? [-180, 180] : lonExt, latD = useGlobal ? [-60, 75] : latExt;
    const x = d3.scaleLinear().domain(lonD).range([10, w - 10]);
    const y = d3.scaleLinear().domain(latD).range([h - 10, 10]);
    // graticule grid
    const gg = svg.append('g').attr('stroke', line()).attr('stroke-opacity', 0.18);
    for (let lon = lonD[0]; lon <= lonD[1]; lon += (lonD[1] - lonD[0]) / 12) gg.append('line').attr('x1', x(lon)).attr('x2', x(lon)).attr('y1', 10).attr('y2', h - 10);
    for (let lat = latD[0]; lat <= latD[1]; lat += (latD[1] - latD[0]) / 8) gg.append('line').attr('y1', y(lat)).attr('y2', y(lat)).attr('x1', 10).attr('x2', w - 10);
    svg.append('rect').attr('x', 1).attr('y', 1).attr('width', w - 2).attr('height', h - 2).attr('fill', 'none').attr('stroke', line()).attr('stroke-width', 2);
    const data = (points || []).map(p => ({ lat: p.lat ?? p.Lat, lon: p.lon ?? p.Lon, name: p.location || p.Location, count: p.count ?? p.Count, sent: p.avgSentiment ?? p.AvgSentiment }));
    const r = d3.scaleSqrt().domain([0, d3.max(data, d => d.count) || 1]).range([4, 30]);
    const sc = v => v > 0.15 ? sentColor('positive') : v < -0.15 ? sentColor('negative') : sentColor('neutral');
    svg.append('g').selectAll('circle').data(data).join('circle')
      .attr('cx', d => x(d.lon)).attr('cy', d => y(d.lat)).attr('r', d => r(d.count))
      .attr('fill', d => sc(d.sent)).attr('fill-opacity', 0.55).attr('stroke', ink()).attr('stroke-width', 1.5)
      .on('mousemove', (ev, d) => showTip(`${d.name}<br><b>${d.count}</b> post · sentimen ${d.sent.toFixed(2)}`, ev)).on('mouseleave', hideTip);
    renderLegend(el, [{ label: 'Positif', color: sentColor('positive') }, { label: 'Netral', color: sentColor('neutral') }, { label: 'Negatif', color: sentColor('negative') }]);
  };

  // ---- Leaflet base-map with sentiment circle markers ----
  let leafletMap = null;
  window.clLeafletMap = function (id, points) {
    const el = document.getElementById(id);
    if (!el || typeof L === 'undefined') return;
    if (leafletMap) { leafletMap.remove(); leafletMap = null; }
    const dark = theme() === 'dark';
    leafletMap = L.map(el, { worldCopyJump: true, scrollWheelZoom: true }).setView([-2.5, 118], 4);
    const tiles = dark
      ? 'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png'
      : 'https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png';
    L.tileLayer(tiles, {
      subdomains: 'abcd', maxZoom: 19,
      attribution: '&copy; OpenStreetMap &copy; CARTO'
    }).addTo(leafletMap);

    const data = (points || []).map(p => ({
      lat: p.lat ?? p.Lat, lon: p.lon ?? p.Lon, name: p.location || p.Location,
      count: p.count ?? p.Count, sent: p.avgSentiment ?? p.AvgSentiment
    }));
    const maxC = Math.max(1, ...data.map(d => d.count));
    for (const d of data) {
      const col = d.sent > 0.15 ? SENT[theme()].positive : d.sent < -0.15 ? SENT[theme()].negative : SENT[theme()].neutral;
      const c = typeof col === 'number' ? '#' + col.toString(16).padStart(6, '0') : col;
      L.circleMarker([d.lat, d.lon], {
        radius: 6 + Math.sqrt(d.count / maxC) * 22,
        color: ink(), weight: 1.5, fillColor: c, fillOpacity: 0.6
      }).bindPopup(`<b>${d.name}</b><br>${d.count} post · sentimen ${Number(d.sent).toFixed(2)}`)
        .addTo(leafletMap);
    }
    setTimeout(() => leafletMap && leafletMap.invalidateSize(), 200);
  };

  window.clLeafletDispose = function () { if (leafletMap) { leafletMap.remove(); leafletMap = null; } };

  function renderLegend(el, items) {
    const wrap = document.createElement('div'); wrap.className = 'legend';
    items.forEach(it => {
      const li = document.createElement('div'); li.className = 'li';
      li.innerHTML = `<span class="sw" style="background:${it.color}"></span>${it.label}`;
      wrap.appendChild(li);
    });
    el.appendChild(wrap);
  }
})();
