import { Component, ViewEncapsulation } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

/**
 * Compiles the real, single-source `styles.scss` (the file `angular.json`
 * loads globally for the whole app) through Angular's own Sass pipeline via
 * `styleUrl`, with `ViewEncapsulation.None` so the selectors are attached
 * unscoped — exactly how they behave as the app's actual global stylesheet.
 * This is test-only plumbing: no CSS is duplicated or hand-copied (so it
 * can't drift from the real file), and no new dependency or Node `fs`
 * access is needed in this browser-targeted test bundle.
 *
 * jsdom parses `@media` blocks into `CSSMediaRule` even though it never
 * *applies* them (it doesn't evaluate print vs. screen), which is enough to
 * assert the selectors exist inside the `@media print` block.
 */
@Component({
  selector: 'app-styles-under-test',
  standalone: true,
  encapsulation: ViewEncapsulation.None,
  template: '',
  styleUrl: './styles.scss',
})
class StylesUnderTestHost {}

describe('Global print styles (styles.scss)', () => {
  let fixture: ComponentFixture<StylesUnderTestHost>;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [StylesUnderTestHost] });
    fixture = TestBed.createComponent(StylesUnderTestHost);
    fixture.detectChanges();
  });

  function findPrintMediaRule(): CSSMediaRule {
    for (const sheet of Array.from(document.styleSheets)) {
      let rules: CSSRuleList;
      try {
        rules = sheet.cssRules;
      } catch {
        continue;
      }
      for (const rule of Array.from(rules)) {
        if (rule instanceof CSSMediaRule && rule.conditionText.includes('print')) {
          return rule;
        }
      }
    }
    throw new Error('No @media print rule found in the loaded stylesheets.');
  }

  function styleRulesIn(rule: CSSMediaRule): CSSStyleRule[] {
    return Array.from(rule.cssRules).filter((r): r is CSSStyleRule => r instanceof CSSStyleRule);
  }

  it('hides the Angular CDK overlay container (snack bars, dialogs, menus, tooltips)', () => {
    const printRule = findPrintMediaRule();
    const overlayRule = styleRulesIn(printRule).find((r) =>
      r.selectorText.includes('.cdk-overlay-container'),
    );

    expect(overlayRule).toBeTruthy();
    expect(overlayRule!.style.display).toBe('none');
  });

  it('hides the CDK overlay backdrop as well', () => {
    const printRule = findPrintMediaRule();
    const selectors = styleRulesIn(printRule).map((r) => r.selectorText);

    expect(selectors.some((s) => s.includes('.cdk-overlay-backdrop'))).toBe(true);
  });

  it('still hides the app shell top navigation bar', () => {
    const printRule = findPrintMediaRule();
    const selectors = styleRulesIn(printRule).map((r) => r.selectorText);

    expect(selectors.some((s) => s.includes('.app-shell__topbar'))).toBe(true);
  });

  it('does not hide app-invoice-print-view or its host selector', () => {
    const printRule = findPrintMediaRule();
    const selectors = styleRulesIn(printRule).map((r) => r.selectorText);

    expect(selectors.some((s) => s.includes('invoice-print-view'))).toBe(false);
  });
});
