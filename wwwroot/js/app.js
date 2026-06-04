$(function () {
  const $progressContainer = $('#progressContainer');
  const $progressBar = $('#progressBar');

  function connectionPayload() {
    return {
      protocol: $('#protocol').val(),
      host: $('#host').val().trim(),
      port: Number($('#port').val()),
      username: $('#username').val(),
      password: $('#password').val(),
      passiveMode: $('#passiveMode').is(':checked')
    };
  }

  function operationPayload(remotePath) {
    return { ...connectionPayload(), remotePath: remotePath };
  }

  function log(message, level = 'info') {
    const timestamp = new Date().toISOString();
    $('#logs').prepend(`<div class="log-entry ${level}">[${timestamp}] ${escapeHtml(message)}</div>`);
  }

  function alert(message, type = 'success') {
    $('#alerts').html(`<div class="alert alert-${type} alert-dismissible fade show" role="alert">${escapeHtml(message)}<button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button></div>`);
  }

  function setLoading($button, loading) {
    $button.prop('disabled', loading);
    $button.data('original-text', $button.data('original-text') || $button.text());
    $button.html(loading ? '<span class="spinner-border spinner-border-sm me-2"></span>Working...' : $button.data('original-text'));
  }

  function showProgress(percent) {
    $progressContainer.removeClass('d-none');
    const value = Math.max(0, Math.min(100, Math.round(percent)));
    $progressBar.css('width', `${value}%`).text(`${value}%`);
  }

  function hideProgress() {
    setTimeout(() => {
      $progressContainer.addClass('d-none');
      $progressBar.css('width', '0%').text('0%');
    }, 750);
  }

  function renderResult(result) {
    $('#resultJson').text(JSON.stringify(result, null, 2));
    const source = result.upload || result.download || result;
    $('#durationMetric').text(`${Math.round(source.durationMilliseconds || result.durationMilliseconds || 0)} ms`);
    const speed = source.bytesPerSecond ? formatBytes(source.bytesPerSecond) + '/s' : '—';
    $('#speedMetric').text(speed);
    $('#statusMetric').html(source.success || result.success ? '<span class="text-success">Success</span>' : '<span class="text-danger">Failure</span>');
  }

  function sendJson(url, payload, $button, onSuccess) {
    setLoading($button, true);
    log(`POST ${url}`);
    return $.ajax({
      url: url,
      method: 'POST',
      contentType: 'application/json',
      data: JSON.stringify(payload)
    }).done(function (data) {
      renderResult(data);
      alert('Operation completed successfully.');
      log(`${url} succeeded`, 'success');
      if (onSuccess) onSuccess(data);
      refreshHistory();
    }).fail(handleAjaxError).always(function () {
      setLoading($button, false);
    });
  }

  function handleAjaxError(xhr) {
    const body = xhr.responseJSON || {};
    const message = body.detail || body.title || body.error || xhr.statusText || 'Request failed.';
    alert(message, 'danger');
    log(message, 'error');
    if (xhr.responseText) $('#resultJson').text(xhr.responseText);
  }

  $('#protocol').on('change', function () {
    const protocol = $(this).val();
    $('#port').val(protocol === 'Sftp' ? 22 : 21);
  });

  $('#connectBtn').on('click', function () {
    sendJson('/api/test-connection', connectionPayload(), $(this));
  });

  $('#listBtn').on('click', function () {
    sendJson('/api/list-directory', operationPayload($('#remotePath').val().trim()), $(this));
  });

  $('#deleteBtn').on('click', function () {
    if (!confirm('Delete the remote file?')) return;
    sendJson('/api/delete-file', operationPayload($('#remotePath').val().trim()), $(this));
  });

  $('#downloadBtn').on('click', function () {
    const $button = $(this);
    setLoading($button, true);
    showProgress(20);
    log('POST /api/download');
    $.ajax({
      url: '/api/download',
      method: 'POST',
      contentType: 'application/json',
      data: JSON.stringify(operationPayload($('#remotePath').val().trim())),
      xhrFields: { responseType: 'blob' },
      xhr: function () {
        const xhr = new XMLHttpRequest();
        xhr.addEventListener('progress', function (evt) {
          if (evt.lengthComputable) showProgress((evt.loaded / evt.total) * 100);
        });
        return xhr;
      }
    }).done(function (blob) {
      showProgress(100);
      const remotePath = $('#remotePath').val().trim();
      const fileName = remotePath.split('/').filter(Boolean).pop() || 'download.bin';
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = fileName;
      link.click();
      URL.revokeObjectURL(url);
      alert('Download completed successfully.');
      log('/api/download succeeded', 'success');
      refreshHistory();
    }).fail(handleAjaxError).always(function () {
      hideProgress();
      setLoading($button, false);
    });
  });

  $('#uploadBtn').on('click', function () {
    const file = $('#uploadFile')[0].files[0];
    if (!file) {
      alert('Choose a file to upload.', 'warning');
      return;
    }

    const payload = connectionPayload();
    const formData = new FormData();
    Object.keys(payload).forEach(key => formData.append(key, payload[key]));
    formData.append('remotePath', $('#uploadRemotePath').val().trim());
    formData.append('file', file);

    const $button = $(this);
    setLoading($button, true);
    log('POST /api/upload');
    $.ajax({
      url: '/api/upload',
      method: 'POST',
      data: formData,
      processData: false,
      contentType: false,
      xhr: function () {
        const xhr = new XMLHttpRequest();
        xhr.upload.addEventListener('progress', function (evt) {
          if (evt.lengthComputable) showProgress((evt.loaded / evt.total) * 100);
        });
        return xhr;
      }
    }).done(function (data) {
      showProgress(100);
      renderResult(data);
      alert('Upload completed successfully.');
      log('/api/upload succeeded', 'success');
      refreshHistory();
    }).fail(handleAjaxError).always(function () {
      hideProgress();
      setLoading($button, false);
    });
  });

  $('#benchmarkBtn').on('click', function () {
    sendJson('/api/benchmark', {
      ...connectionPayload(),
      remoteDirectory: $('#benchmarkDirectory').val().trim(),
      fileSizeBytes: Number($('#benchmarkSize').val())
    }, $(this));
  });

  $('#refreshHistoryBtn').on('click', refreshHistory);

  function refreshHistory() {
    $.getJSON('/api/history').done(function (items) {
      if (!items.length) {
        $('#history').html('<div class="text-muted">No history yet.</div>');
        return;
      }
      $('#history').html(items.slice(0, 20).map(item => {
        const success = item.success === true ? 'success' : 'danger';
        const operation = item.operation || 'benchmark';
        const duration = Math.round(item.durationMilliseconds || 0);
        return `<div class="border-bottom py-2"><span class="badge text-bg-${success}">${success}</span> <strong>${escapeHtml(operation)}</strong> ${escapeHtml(item.protocol || '')} ${duration} ms<br><span class="text-muted">${escapeHtml(item.host || '')} · ${escapeHtml(item.timestampUtc || '')}</span></div>`;
      }).join(''));
    });
  }

  function formatBytes(bytes) {
    if (!bytes) return '0 B';
    const units = ['B', 'KB', 'MB', 'GB'];
    let value = bytes;
    let index = 0;
    while (value >= 1024 && index < units.length - 1) {
      value /= 1024;
      index++;
    }
    return `${value.toFixed(2)} ${units[index]}`;
  }

  function escapeHtml(value) {
    return String(value ?? '').replace(/[&<>'"]/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[char]));
  }

  refreshHistory();
  log('Dashboard loaded.', 'success');
});
