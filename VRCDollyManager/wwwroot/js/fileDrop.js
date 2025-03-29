window.initAppDropMonitoring = function(dotNetHelper) {
    const appElement = document.getElementById('app');

    if (!appElement) {
        console.error("Element with id 'app' not found.");
        return;
    }

    appElement.addEventListener('dragover', (event) => {
        event.preventDefault(); // Prevent default to allow drop
    });

    appElement.addEventListener('drop', async (event) => {
        event.preventDefault();


        if (event.dataTransfer.files.length > 0) {
            const file = event.dataTransfer.files[0];
            const reader = new FileReader();

            reader.onload = async (e) => {
                const fileContent = e.target.result;
                console.debug("File content:", fileContent);

                // Send serialized file content to Blazor
                await dotNetHelper.invokeMethodAsync("OnFileDropped", fileContent);
            };

            reader.readAsText(file); // Reads the file content as text (JSON compatible)
        }
    });
};