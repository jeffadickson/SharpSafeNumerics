window.decimalLab = {
    setup(initial) {
        const fallback = document.getElementById('case-input');
        if (!window.require?.config || !fallback) return;
        window.require.config({ paths: { vs: 'https://cdn.jsdelivr.net/npm/monaco-editor@0.52.2/min/vs' } });
        window.require(['vs/editor/editor.main'], () => {
            const container = document.getElementById('case-editor');
            this.editor = window.monaco.editor.create(container, {
                value: fallback.value || initial, theme: 'vs-dark',
                minimap: { enabled: false }, automaticLayout: true,
                ariaLabel: 'Two decimal operands and a rounding mode'
            });
            fallback.hidden = true;
            document.querySelector('label[for="case-input"]').hidden = true;
        });
    },
    read() {
        return this.editor?.getValue() ?? document.getElementById('case-input').value;
    }
};
