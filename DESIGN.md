---
name: YourSafe website
description: A clear software field manual for a local Windows vault; applies only to website/.
colors:
  primary: "#204bdb"
  primary-deep: "#1639ab"
  paper: "#f5f6f2"
  ink: "#182321"
  muted: "#505d58"
  rule: "#cbd2ca"
  tint: "#e9ede5"
  white: "#ffffff"
typography:
  display:
    fontFamily: "Be Vietnam Pro, sans-serif"
    fontSize: "clamp(3.1rem, 7.1vw, 6rem)"
    fontWeight: 700
    lineHeight: 1.12
    letterSpacing: "-0.04em"
  heading:
    fontFamily: "Be Vietnam Pro, sans-serif"
    fontSize: "clamp(1.9rem, 3.6vw, 3.4rem)"
    fontWeight: 700
    lineHeight: 1.22
    letterSpacing: "-0.035em"
  body:
    fontFamily: "Be Vietnam Pro, sans-serif"
    fontSize: "1rem"
    fontWeight: 400
    lineHeight: 1.75
rounded:
  control: "4px"
spacing:
  small: "1rem"
  medium: "2rem"
  large: "3rem"
components:
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.white}"
    rounded: "{rounded.control}"
    padding: "1rem 1.35rem"
  button-primary-hover:
    backgroundColor: "{colors.primary-deep}"
    textColor: "{colors.white}"
---

## Overview

This system describes the static YourSafe download website only, not the WinUI application. Cool paper, cobalt fields and clear Vietnamese typography create the feel of a useful software manual. Real application assets carry identity; the geometric eye logo remains unchanged.

## Colors

Cobalt marks actions, the manual's contents panel and the final download section. Cool paper is the reading surface. Ink is the primary text; muted ink is used for supporting prose. A pale green neutral separates the historical application image from the surrounding sections.

**The One Action Color Rule.** Cobalt leads action; the app logo retains its original colors.

## Typography

Self-hosted Be Vietnam Pro uses regular and bold weights. The product name is the largest text; section headings come next, then feature titles and body. Code samples use Consolas with a monospace fallback. Supporting text remains at least 12.8px. Mobile body text in dense instructions is 14.4px.

## Layout

Full-width section grounds contain generous inner gutters. The desktop cover, feature rows and introductory headings use two columns. Feature lists are divided by rules instead of cards. At 680px they stack; at 960px the image layout becomes more compact. Sequential installation instructions retain their numbered left rail on mobile.

**The Numbered Steps Rule.** Numbers belong to the installation sequence, not decorative section labels.

## Elevation & Depth

The interface is mostly flat. Only the real application image receives a soft, offset shadow (0 14px 35px). Controls use color and motion for hover feedback instead of shadows.

## Shapes

The manual is rectangular. Small 4px corners soften buttons and the command sample. The actual eye logo is not redrawn or recolored.

## Components

Buttons use clear action text with a small stroke SVG. Desktop navigation is inline; mobile navigation collapses only after JavaScript loads. Focus uses a 3px cobalt outline with a 5px offset, white on the cobalt CTA. Native details/summary provides the FAQ without JavaScript. The clipboard button appears only when the browser supports the secure Clipboard API and reports both success and failure.

The cover enters with a slight vertical move and a restrained clip reveal of the manual panel. Feature rows move gently once when entering view. Content is never hidden waiting for JavaScript. Reduced motion disables animation, transitions and smooth scroll.

## Do's and Don'ts

- Do keep download guidance factual and link to the repository's actual release list.
- Do preserve the packaged logo and label the screenshot as a historical pre-rename capture.
- Do keep navigation, FAQ and download actions usable with JavaScript disabled.
- Don't portray the manual panel as a screenshot or fabricate a current application UI.
- Don't add ratings, security certifications, cloud features or signing claims without evidence.
