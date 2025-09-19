window.initializeFolderUpload = () => {
    const fileInput = document.getElementById('folderInput');
    if (fileInput) {
        // Set the webkitdirectory attribute for folder selection
        fileInput.setAttribute('webkitdirectory', '');
        fileInput.setAttribute('directory', '');
        fileInput.setAttribute('multiple', '');
    }
};