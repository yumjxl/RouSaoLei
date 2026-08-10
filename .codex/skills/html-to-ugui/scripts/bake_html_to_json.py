#!/usr/bin/env python3
"""Bake UI-DSL HTML into Unity HtmlToUGUI JSON.

The output keeps the original v1 fields consumed by HtmlToUGUIBaker and adds
schemaVersion=2 metadata for image binding and adaptive Prefab generation.
"""

import argparse
import json
import os
import sys


def bake_html_to_json(
    html_content: str,
    width: int = 1920,
    height: int = 1080,
    source_path: str = "",
) -> dict:
    try:
        from playwright.sync_api import sync_playwright
    except ImportError:
        print(
            "Error: playwright is required. Run: pip install playwright && playwright install chromium",
            file=sys.stderr,
        )
        sys.exit(1)

    injected_html = json.dumps(html_content)
    full_html = f"""<!DOCTYPE html>
<html><head><meta charset="UTF-8">
<style>
body {{ margin: 0; padding: 0; }}
#canvas-sandbox {{ position: relative; width: {width}px; height: {height}px; }}
#canvas-sandbox * {{ box-sizing: border-box !important; }}
#canvas-sandbox [data-u-type] {{ min-width: 0; min-height: 0; }}
</style>
</head><body>
<div id="canvas-sandbox"></div>
<script>
const sandbox = document.getElementById('canvas-sandbox');
sandbox.innerHTML = {injected_html};

function rgb2hex(rgb) {{
    if (!rgb || rgb === 'rgba(0, 0, 0, 0)' || rgb === 'transparent') return '#FFFFFF00';
    const match = rgb.match(/^rgba?\\((\\d+),\\s*(\\d+),\\s*(\\d+)(?:,\\s*([\\d.]+))?\\)$/);
    if (!match) return '#FFFFFF';
    const r = ('0' + parseInt(match[1], 10).toString(16)).slice(-2);
    const g = ('0' + parseInt(match[2], 10).toString(16)).slice(-2);
    const b = ('0' + parseInt(match[3], 10).toString(16)).slice(-2);
    const a = match[4] ? ('0' + Math.round(parseFloat(match[4]) * 255).toString(16)).slice(-2) : 'ff';
    return `#${{r}}${{g}}${{b}}${{a === 'ff' ? '' : a}}`;
}}

function firstCssUrl(value) {{
    if (!value || value === 'none') return '';
    const match = value.match(/url\\((['"]?)(.*?)\\1\\)/);
    return match ? match[2] : '';
}}

function readAttr(element, names) {{
    for (const name of names) {{
        const value = element.getAttribute(name);
        if (value !== null && value !== '') return value;
    }}
    return '';
}}

function inferLayoutHint(element, rect, parentRect, style) {{
    const explicit = readAttr(element, ['data-u-layout', 'data-u-anchor']);
    if (explicit) return explicit;

    const left = rect.left - parentRect.left;
    const top = rect.top - parentRect.top;
    const right = parentRect.right - rect.right;
    const bottom = parentRect.bottom - rect.bottom;
    const tol = Math.max(2, Math.min(parentRect.width, parentRect.height) * 0.01);

    if (left <= tol && top <= tol && right <= tol && bottom <= tol) return 'stretch';
    if (top <= tol && left <= tol && right <= tol) return 'top-bar';
    if (bottom <= tol && left <= tol && right <= tol) return 'bottom-bar';
    if (left <= tol && top <= tol && bottom <= tol) return 'left-panel';
    if (right <= tol && top <= tol && bottom <= tol) return 'right-panel';

    const centerX = left + rect.width * 0.5;
    const centerY = top + rect.height * 0.5;
    if (Math.abs(centerX - parentRect.width * 0.5) <= tol * 2 &&
        Math.abs(centerY - parentRect.height * 0.5) <= tol * 2 &&
        rect.width < parentRect.width * 0.95 &&
        rect.height < parentRect.height * 0.95) {{
        return 'center';
    }}

    const inlineStyle = element.style;
    const hasHorizontalEdges = !!inlineStyle.left && !!inlineStyle.right && inlineStyle.right !== 'auto';
    const hasVerticalEdges = !!inlineStyle.top && !!inlineStyle.bottom && inlineStyle.bottom !== 'auto';
    if (hasHorizontalEdges && hasVerticalEdges) return 'stretch';
    if (hasHorizontalEdges) return top <= parentRect.height * 0.5 ? 'stretch-x-top' : 'stretch-x-bottom';
    if (hasVerticalEdges) return left <= parentRect.width * 0.5 ? 'stretch-y-left' : 'stretch-y-right';

    return 'fixed';
}}

function traverseAndBake(element, rootRect, parentRect) {{
    const uType = element.getAttribute('data-u-type');
    const uName = element.getAttribute('data-u-name');
    let nodeData = null;

    if (uType && uName) {{
        const rect = element.getBoundingClientRect();
        const style = window.getComputedStyle(element);
        const relativeX = rect.left - rootRect.left;
        const relativeY = rect.top - rootRect.top;
        const realWidth = rect.width;
        const realHeight = rect.height;

        let textContent = element.innerText || '';
        if (element.tagName.toLowerCase() === 'input') {{
            textContent = element.value || element.placeholder || '';
        }}

        let fontSize = 14;
        if (style.fontSize) fontSize = parseFloat(style.fontSize);

        const backgroundImageSrc = firstCssUrl(style.backgroundImage);
        const imageSrc = readAttr(element, ['data-u-src', 'src']);
        const imageFit = readAttr(element, ['data-u-fit']) || style.objectFit || style.backgroundSize || '';
        const layoutHint = inferLayoutHint(element, rect, parentRect || rootRect, style);
        const safeArea = readAttr(element, ['data-u-safe-area']);
        const uDir = element.getAttribute('data-u-dir') || 'v';
        const uValue = parseFloat(element.getAttribute('data-u-value')) || 0.5;
        const uChecked = element.getAttribute('data-u-checked') === 'true';
        const uOptions = [];

        if (uType === 'dropdown' && element.tagName.toLowerCase() === 'select') {{
            const opts = element.querySelectorAll('option');
            opts.forEach(opt => uOptions.push(opt.innerText.trim()));
        }}

        nodeData = {{
            schemaVersion: 2,
            name: uName,
            type: uType,
            dir: uDir,
            value: uValue,
            isChecked: uChecked,
            options: uOptions,
            x: Math.round(relativeX),
            y: Math.round(relativeY),
            width: Math.round(realWidth),
            height: Math.round(realHeight),
            color: rgb2hex(style.backgroundColor),
            fontColor: rgb2hex(style.color),
            fontSize: Math.round(fontSize),
            textAlign: style.textAlign || 'center',
            text: textContent.trim(),
            imageSrc: imageSrc,
            backgroundImageSrc: backgroundImageSrc,
            imageFit: imageFit,
            layoutHint: layoutHint,
            safeArea: safeArea,
            anchorPreset: readAttr(element, ['data-u-anchor']),
            anchorMin: readAttr(element, ['data-u-anchor-min']),
            anchorMax: readAttr(element, ['data-u-anchor-max']),
            pivot: readAttr(element, ['data-u-pivot']),
            offsetMin: readAttr(element, ['data-u-offset-min']),
            offsetMax: readAttr(element, ['data-u-offset-max']),
            cssPosition: style.position,
            cssLeft: style.left,
            cssRight: style.right,
            cssTop: style.top,
            cssBottom: style.bottom,
            cssWidth: style.width,
            cssHeight: style.height,
            cssObjectFit: style.objectFit,
            cssBackgroundSize: style.backgroundSize,
            children: []
        }};
    }}

    const childrenData = [];
    const nextParentRect = nodeData ? element.getBoundingClientRect() : parentRect;
    for (let i = 0; i < element.children.length; i++) {{
        if (element.tagName.toLowerCase() === 'select' && element.children[i].tagName.toLowerCase() === 'option') {{
            continue;
        }}
        const childResult = traverseAndBake(element.children[i], rootRect, nextParentRect);
        if (childResult) childrenData.push(childResult);
    }}

    if (nodeData) {{
        nodeData.children = childrenData;
        return nodeData;
    }}

    if (childrenData.length > 0) {{
        return childrenData.length === 1 ? childrenData[0] : {{
            schemaVersion: 2,
            name: 'layoutGroup_' + Math.random().toString(36).substr(2, 5),
            type: 'div',
            dir: 'v',
            value: 0,
            isChecked: false,
            options: [],
            x: 0,
            y: 0,
            width: 0,
            height: 0,
            color: '#FFFFFF00',
            fontColor: '#000000',
            fontSize: 14,
            textAlign: 'center',
            text: '',
            layoutHint: 'fixed',
            children: childrenData
        }};
    }}

    return null;
}}

const rootElement = sandbox.querySelector('[data-u-name]');
if (!rootElement) {{
    throw new Error('No root node with data-u-name was found.');
}}
const rootRect = rootElement.getBoundingClientRect();
const result = traverseAndBake(rootElement, rootRect, sandbox.getBoundingClientRect());
result.schemaVersion = 2;
result.designWidth = {width};
result.designHeight = {height};
window.__BAKE_RESULT__ = result;
</script>
</body></html>"""

    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        page = browser.new_page(viewport={"width": width + 100, "height": height + 100})
        page.set_content(full_html, wait_until="networkidle")
        page.wait_for_timeout(250)
        result = page.evaluate("window.__BAKE_RESULT__")
        browser.close()

    if result is None:
        raise ValueError("Bake failed: no valid UI-DSL node was found.")

    if source_path:
        full_source = os.path.abspath(source_path)
        result["sourcePath"] = full_source
        result["htmlFilePath"] = full_source
        result["sourceDirectory"] = os.path.dirname(full_source)

    return result


def main():
    parser = argparse.ArgumentParser(description="Bake UI-DSL HTML into HtmlToUGUI JSON")
    parser.add_argument("input", help="Input HTML file path")
    parser.add_argument("-o", "--output", help="Output JSON path. Defaults to same name with .json")
    parser.add_argument("-w", "--width", type=int, default=1920, help="Design canvas width")
    parser.add_argument("-H", "--height", type=int, default=1080, help="Design canvas height")
    parser.add_argument("--stdout", action="store_true", help="Print JSON instead of writing a file")
    args = parser.parse_args()

    if not os.path.exists(args.input):
        print(f"Error: file does not exist: {args.input}", file=sys.stderr)
        sys.exit(1)

    with open(args.input, "r", encoding="utf-8") as f:
        html_content = f.read()

    result = bake_html_to_json(html_content, args.width, args.height, args.input)
    json_str = json.dumps(result, ensure_ascii=False, indent=2)

    if args.stdout:
        print(json_str)
    else:
        output_path = args.output or os.path.splitext(args.input)[0] + ".json"
        with open(output_path, "w", encoding="utf-8") as f:
            f.write(json_str)
        print(f"Bake complete: {output_path}")


if __name__ == "__main__":
    main()
