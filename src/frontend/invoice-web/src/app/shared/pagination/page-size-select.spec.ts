import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PageSizeSelect } from './page-size-select';

describe('PageSizeSelect', () => {
  let fixture: ComponentFixture<PageSizeSelect>;
  let component: PageSizeSelect;

  function setInputs(overrides: Partial<PageSizeSelect> = {}): void {
    TestBed.configureTestingModule({ imports: [PageSizeSelect] });
    fixture = TestBed.createComponent(PageSizeSelect);
    component = fixture.componentInstance;
    Object.assign(component, { pageSize: 5, ...overrides });
    fixture.detectChanges();
  }

  it('should associate the label with the select', () => {
    setInputs();

    const label: HTMLLabelElement = fixture.nativeElement.querySelector('label');
    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select');
    expect(label.getAttribute('for')).toBe(select.id);
    expect(label.textContent).toContain('Itens por página');
  });

  it('should render all page size options', () => {
    setInputs();

    const options: HTMLOptionElement[] = Array.from(
      fixture.nativeElement.querySelectorAll('option'),
    );
    expect(options.map((option) => option.value)).toEqual(['5', '10', '25', '50']);
  });

  it('should reflect the bound pageSize as the selected option, regardless of its position in the options list', () => {
    setInputs({ pageSize: 25 });

    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select');
    expect(select.value).toBe('25');

    const selectedOption: HTMLOptionElement =
      fixture.nativeElement.querySelector('option[value="25"]');
    expect(selectedOption.selected).toBe(true);
  });

  it('should default to reflecting pageSize=5 when that is the bound value', () => {
    setInputs({ pageSize: 5 });

    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select');
    expect(select.value).toBe('5');
  });

  it('should emit pageSizeChange when a new page size is selected', () => {
    setInputs();
    const emitted: number[] = [];
    component.pageSizeChange.subscribe((value) => emitted.push(value));

    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select');
    select.value = '25';
    select.dispatchEvent(new Event('change'));

    expect(emitted).toEqual([25]);
  });

  it('should not emit pageSizeChange when the selected value equals the current pageSize', () => {
    setInputs({ pageSize: 10 });
    const emitted: number[] = [];
    component.pageSizeChange.subscribe((value) => emitted.push(value));

    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select');
    select.value = '10';
    select.dispatchEvent(new Event('change'));

    expect(emitted).toEqual([]);
  });

  it('should disable the select while loading', () => {
    setInputs({ loading: true });

    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select');
    expect(select.disabled).toBe(true);
  });
});
